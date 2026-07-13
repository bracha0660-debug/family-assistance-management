using System.Globalization;
using System.Text.Json;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;

namespace FamilyAssistance.Api.Services;

/// <summary>
/// Single server-side composition point for the Home Dashboard widget list.
/// Visibility uses effective grants, scope, and FullOrgAccess only — never role names.
/// Phase 14: KPI counts are item-based (except drafts); navigation includes listView.
/// </summary>
public sealed class HomeWidgetComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo HebrewCulture = CultureInfo.GetCultureInfo("he-IL");
    private const int MonthlyTrendMonths = 6;
    private const int StaleSubmittedDays = 7;
    private const int StaleSuspendedDays = 30;
    private const int StaleAwaitingPaymentDays = 14;
    public const int RecentActivityQueryLimit = 40;
    private const int RecentActivityLimit = 8;

    public const string ListViewDraftDecisions = "draft_decisions";
    public const string ListViewAssistanceItems = "assistance_items";

    public HomeDashboardDto Compose(
        AuthorizationContext auth,
        IReadOnlyList<CommitteeDecision> scopedDecisions,
        IReadOnlyList<PaymentExecution> scopedPayments,
        IReadOnlyList<AuditLog> scopedActivityLogs)
    {
        var widgets = new List<HomeWidgetDto>();

        if (!HasHomeDashboardAccess(auth))
        {
            return new HomeDashboardDto
            {
                GeneratedAt = DateTime.UtcNow,
                Widgets = widgets
            };
        }

        var scopedItems = scopedDecisions
            .SelectMany(d => d.Items.Select(i => (Item: i, Decision: d)))
            .ToList();

        var kpiCards = BuildKpiCards(auth, scopedDecisions, scopedItems);
        if (kpiCards.Count > 0)
        {
            widgets.Add(new HomeWidgetDto
            {
                Id = "operational_kpis",
                Type = HomeWidgetTypes.KpiCards,
                Title = string.Empty,
                Data = SerializeData(new HomeKpiCardsDataDto { Cards = kpiCards })
            });
        }

        var financialMetrics = BuildFinancialSummary(auth, scopedItems, scopedPayments);
        if (financialMetrics.Count > 0)
        {
            widgets.Add(new HomeWidgetDto
            {
                Id = "financial_summary",
                Type = HomeWidgetTypes.FinancialSummary,
                Title = "תמונת מצב כספית",
                Data = SerializeData(new HomeFinancialSummaryDataDto { Metrics = financialMetrics })
            });
        }

        var bottlenecks = BuildBottlenecks(auth, scopedItems);
        if (bottlenecks is not null)
        {
            widgets.Add(new HomeWidgetDto
            {
                Id = "bottlenecks",
                Type = HomeWidgetTypes.Bottlenecks,
                Title = "צווארי בקבוק",
                Data = SerializeData(bottlenecks)
            });
        }

        var monthlyTrend = BuildMonthlyTrend(auth, scopedItems);
        if (monthlyTrend is not null)
        {
            widgets.Add(new HomeWidgetDto
            {
                Id = "monthly_trend",
                Type = HomeWidgetTypes.MonthlyTrend,
                Title = "מגמה חודשית",
                Data = SerializeData(monthlyTrend)
            });
        }

        var recentActivity = BuildRecentActivity(auth, scopedDecisions, scopedPayments, scopedActivityLogs);
        if (recentActivity is not null)
        {
            widgets.Add(new HomeWidgetDto
            {
                Id = "recent_activity",
                Type = HomeWidgetTypes.RecentActivity,
                Title = "פעילות אחרונה",
                Data = SerializeData(recentActivity)
            });
        }

        return new HomeDashboardDto
        {
            GeneratedAt = DateTime.UtcNow,
            Widgets = widgets
        };
    }

    private static List<HomeKpiCardDto> BuildKpiCards(
        AuthorizationContext auth,
        IReadOnlyList<CommitteeDecision> scopedDecisions,
        IReadOnlyList<(AssistanceItem Item, CommitteeDecision Decision)> scopedItems)
    {
        var cards = new List<HomeKpiCardDto>();
        var mineScope = UsesMyRecordsCommitteeScope(auth);

        if (CanShowDraftsKpi(auth))
        {
            var draftCount = scopedDecisions.Count(d =>
                d.Status == CommitteeDecisionStatuses.Draft
                && d.CreatedByUserId == auth.UserId);

            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "drafts",
                Title = "טיוטות",
                Subtitle = "החלטות בטיוטה",
                Count = draftCount,
                StatusSemantic = HomeWorkflowStatus.Draft,
                NavigationTarget = DecisionNav(
                    listView: ListViewDraftDecisions,
                    status: CommitteeDecisionStatuses.Draft,
                    ownership: "mine")
            });
        }

        if (CanShowCommitteeStatusKpi(auth))
        {
            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "awaiting_approval",
                Title = "ממתין לאישור",
                Subtitle = "פריטים שהוגשו",
                Count = scopedItems.Count(x => x.Item.Status == AssistanceItemStatuses.Submitted),
                StatusSemantic = HomeWorkflowStatus.PendingApproval,
                NavigationTarget = ItemNav(AssistanceItemStatuses.Submitted)
            });

            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "returned_for_revision",
                Title = "הוחזר לטיפול",
                Subtitle = "פריטים שהוחזרו",
                Count = scopedItems.Count(x => x.Item.Status == AssistanceItemStatuses.Returned),
                StatusSemantic = HomeWorkflowStatus.ReturnedForTreatment,
                NavigationTarget = ItemNav(AssistanceItemStatuses.Returned)
            });

            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "suspended",
                Title = "בהשהיה",
                Subtitle = "פריטים בהשהיה",
                Count = scopedItems.Count(x => x.Item.Status == AssistanceItemStatuses.Suspended),
                StatusSemantic = HomeWorkflowStatus.OnHold,
                NavigationTarget = ItemNav(AssistanceItemStatuses.Suspended)
            });
        }

        if (CanShowAwaitingExecutionKpi(auth))
        {
            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "awaiting_execution",
                Title = "ממתין לתשלום",
                Subtitle = "פריטים ממתינים לאסמכתא",
                Count = scopedItems.Count(x => x.Item.Status == AssistanceItemStatuses.WaitingForReference),
                StatusSemantic = HomeWorkflowStatus.PendingExecution,
                NavigationTarget = PaymentNav("finance_waiting_for_reference")
            });
        }

        return cards;
    }

    private static List<HomeFinancialMetricDto> BuildFinancialSummary(
        AuthorizationContext auth,
        IReadOnlyList<(AssistanceItem Item, CommitteeDecision Decision)> scopedItems,
        IReadOnlyList<PaymentExecution> scopedPayments)
    {
        if (!CanShowFinancialSummary(auth))
            return [];

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var approvedThisMonth = scopedItems
            .Where(x =>
                (x.Item.Status is AssistanceItemStatuses.Approved
                    or AssistanceItemStatuses.WaitingForReference
                    or AssistanceItemStatuses.Paid
                    or AssistanceItemStatuses.Completed)
                && x.Item.ApprovedAt is not null
                && x.Item.ApprovedAt.Value >= monthStart
                && x.Item.ApprovedAt.Value < monthEnd)
            .Sum(x => x.Item.Amount);

        var paidThisMonth = scopedItems
            .Where(x =>
                (x.Item.Status is AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed)
                && x.Item.PaymentExecution?.PaidAt is not null
                && x.Item.PaymentExecution.PaidAt.Value >= monthStart
                && x.Item.PaymentExecution.PaidAt.Value < monthEnd)
            .Sum(x => x.Item.Amount);

        // Fallback for legacy payments without item ApprovedAt path
        if (paidThisMonth == 0)
        {
            paidThisMonth = scopedPayments
                .Where(p => p.PaidAt is not null
                    && p.PaidAt.Value >= monthStart
                    && p.PaidAt.Value < monthEnd)
                .Sum(p => p.AssistanceItem?.Amount ?? 0m);
        }

        var awaitingExecution = scopedItems
            .Where(x => x.Item.Status == AssistanceItemStatuses.WaitingForReference)
            .Sum(x => x.Item.Amount);

        var onHoldTotal = scopedItems
            .Where(x => x.Item.Status == AssistanceItemStatuses.Suspended)
            .Sum(x => x.Item.Amount);

        return
        [
            new HomeFinancialMetricDto
            {
                MetricKey = "approved_this_month",
                Title = "אושר החודש",
                Amount = approvedThisMonth,
                StatusSemantic = HomeWorkflowStatus.Paid,
                NavigationTarget = ItemNav(AssistanceItemStatuses.Approved)
            },
            new HomeFinancialMetricDto
            {
                MetricKey = "paid_this_month",
                Title = "שולם החודש",
                Amount = paidThisMonth,
                StatusSemantic = HomeWorkflowStatus.Paid,
                NavigationTarget = ItemNav(AssistanceItemStatuses.Paid)
            },
            new HomeFinancialMetricDto
            {
                MetricKey = "awaiting_execution",
                Title = "ממתין לתשלום",
                Amount = awaitingExecution,
                StatusSemantic = HomeWorkflowStatus.PendingExecution,
                NavigationTarget = PaymentNav("finance_waiting_for_reference")
            },
            new HomeFinancialMetricDto
            {
                MetricKey = "suspended",
                Title = "בהשהיה",
                Amount = onHoldTotal,
                StatusSemantic = HomeWorkflowStatus.OnHold,
                NavigationTarget = ItemNav(AssistanceItemStatuses.Suspended)
            }
        ];
    }

    private static HomeMonthlyTrendDataDto? BuildMonthlyTrend(
        AuthorizationContext auth,
        IReadOnlyList<(AssistanceItem Item, CommitteeDecision Decision)> scopedItems)
    {
        if (!CanShowFinancialSummary(auth))
            return null;

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var points = new List<HomeMonthlyTrendPointDto>(MonthlyTrendMonths);

        for (var offset = MonthlyTrendMonths - 1; offset >= 0; offset--)
        {
            var monthStart = currentMonthStart.AddMonths(-offset);
            var monthEnd = monthStart.AddMonths(1);
            var amount = scopedItems
                .Where(x => x.Item.ApprovedAt is not null
                    && x.Item.ApprovedAt.Value >= monthStart
                    && x.Item.ApprovedAt.Value < monthEnd)
                .Sum(x => x.Item.Amount);

            points.Add(new HomeMonthlyTrendPointDto
            {
                MonthKey = monthStart.ToString("yyyy-MM"),
                LabelHe = FormatHebrewMonthLabel(monthStart),
                Amount = amount
            });
        }

        return new HomeMonthlyTrendDataDto
        {
            Subtitle = "סכום שאושר (₪)",
            Points = points
        };
    }

    private static HomeBottlenecksDataDto? BuildBottlenecks(
        AuthorizationContext auth,
        IReadOnlyList<(AssistanceItem Item, CommitteeDecision Decision)> scopedItems)
    {
        var alerts = new List<HomeBottleneckAlertDto>();
        var now = DateTime.UtcNow;

        if (HasCommitteeView(auth))
        {
            var submittedCutoff = now.AddDays(-StaleSubmittedDays);
            alerts.Add(new HomeBottleneckAlertDto
            {
                AlertKey = "stale_submitted",
                Title = "פריטים ממתינים מעל 7 ימים",
                Description = "פריטים שהוגשו וממתינים לאישור מעל 7 ימים",
                Count = scopedItems.Count(x =>
                    x.Item.Status == AssistanceItemStatuses.Submitted
                    && x.Decision.SubmittedAt is not null
                    && x.Decision.SubmittedAt < submittedCutoff),
                ThresholdDays = StaleSubmittedDays,
                StatusSemantic = HomeWorkflowStatus.PendingApproval,
                NavigationTarget = WithMinAge(ItemNav(AssistanceItemStatuses.Submitted), StaleSubmittedDays)
            });

            var suspendedCutoff = now.AddDays(-StaleSuspendedDays);
            alerts.Add(new HomeBottleneckAlertDto
            {
                AlertKey = "stale_suspended",
                Title = "פריטים בהשהיה מעל 30 יום",
                Description = "פריטים בהשהיה מעל 30 יום",
                Count = scopedItems.Count(x =>
                    x.Item.Status == AssistanceItemStatuses.Suspended
                    && x.Item.UpdatedAt < suspendedCutoff),
                ThresholdDays = StaleSuspendedDays,
                StatusSemantic = HomeWorkflowStatus.OnHold,
                NavigationTarget = WithMinAge(ItemNav(AssistanceItemStatuses.Suspended), StaleSuspendedDays)
            });
        }

        if (auth.FullOrgAccess || auth.HasGrant(PermissionKeys.PaymentsView)
            || auth.HasGrant(PermissionKeys.PaymentsExportBatchesCreate)
            || auth.HasGrant(PermissionKeys.PaymentsExecute))
        {
            var paymentCutoff = now.AddDays(-StaleAwaitingPaymentDays);
            alerts.Add(new HomeBottleneckAlertDto
            {
                AlertKey = "stale_awaiting_payment",
                Title = "פריטים ממתינים לאסמכתא מעל 14 יום",
                Description = "פריטים ממתינים לאסמכתא מעל 14 יום",
                Count = scopedItems.Count(x =>
                    x.Item.Status == AssistanceItemStatuses.WaitingForReference
                    && x.Item.UpdatedAt < paymentCutoff),
                ThresholdDays = StaleAwaitingPaymentDays,
                StatusSemantic = HomeWorkflowStatus.PendingExecution,
                NavigationTarget = WithMinAge(PaymentNav("finance_waiting_for_reference"), StaleAwaitingPaymentDays)
            });
        }

        return alerts.Count == 0 ? null : new HomeBottlenecksDataDto { Alerts = alerts };
    }

    private static HomeRecentActivityDataDto? BuildRecentActivity(
        AuthorizationContext auth,
        IReadOnlyList<CommitteeDecision> scopedDecisions,
        IReadOnlyList<PaymentExecution> scopedPayments,
        IReadOnlyList<AuditLog> scopedActivityLogs)
    {
        if (!CanShowRecentActivity(auth) || scopedActivityLogs.Count == 0)
            return null;

        var decisionsById = scopedDecisions.ToDictionary(d => d.Id);
        var paymentsById = scopedPayments.ToDictionary(p => p.Id);
        var mineScope = UsesMyRecordsCommitteeScope(auth);
        var hasApprove = auth.FullOrgAccess || auth.HasGrant(PermissionKeys.CommitteeDecisionsApprove);
        var entries = new List<HomeRecentActivityEntryDto>(RecentActivityLimit);

        foreach (var log in scopedActivityLogs.OrderByDescending(l => l.CreatedAt))
        {
            if (!HomeActivityPresentation.IsDisplayableActivity(log))
                continue;

            var (statusLabel, statusSemantic, workflowStatus) = HomeActivityPresentation.Resolve(log);
            string decisionCode;
            string familyName;
            HomeNavigationTargetDto? navigationTarget;

            if (log.EntityType == "committee_decision")
            {
                if (!decisionsById.TryGetValue(log.EntityId, out var decision))
                    continue;

                decisionCode = decision.DecisionCode;
                familyName = decision.Family?.FamilyLastName ?? string.Empty;
                navigationTarget = workflowStatus is not null
                    ? ResolveDecisionActivityNavigation(mineScope, hasApprove, workflowStatus)
                    : null;
            }
            else if (log.EntityType == "payment_execution")
            {
                if (!paymentsById.TryGetValue(log.EntityId, out var payment))
                    continue;

                decisionCode = payment.CommitteeDecision?.DecisionCode ?? string.Empty;
                familyName = payment.CommitteeDecision?.Family?.FamilyLastName ?? string.Empty;
                navigationTarget = workflowStatus is not null
                    ? ResolvePaymentActivityNavigation(workflowStatus)
                    : null;
            }
            else
            {
                continue;
            }

            entries.Add(new HomeRecentActivityEntryDto
            {
                EntryKey = log.Id.ToString(),
                DecisionCode = decisionCode,
                FamilyName = familyName,
                StatusLabel = statusLabel,
                StatusSemantic = statusSemantic,
                OccurredAt = log.CreatedAt,
                ActorName = log.ActorUser?.FullName,
                NavigationTarget = navigationTarget
            });

            if (entries.Count >= RecentActivityLimit)
                break;
        }

        return entries.Count == 0 ? null : new HomeRecentActivityDataDto { Entries = entries };
    }

    private static HomeNavigationTargetDto? ResolveDecisionActivityNavigation(
        bool mineScope,
        bool hasApprove,
        string status) =>
        status switch
        {
            CommitteeDecisionStatuses.Draft => DecisionNav(
                listView: ListViewDraftDecisions,
                status: CommitteeDecisionStatuses.Draft,
                ownership: mineScope ? "mine" : null),
            CommitteeDecisionStatuses.Submitted => ItemNav(AssistanceItemStatuses.Submitted),
            CommitteeDecisionStatuses.ReturnedForRevision => ItemNav(AssistanceItemStatuses.Returned),
            CommitteeDecisionStatuses.Suspended => ItemNav(AssistanceItemStatuses.Suspended),
            CommitteeDecisionStatuses.Approved or CommitteeDecisionStatuses.PartiallyPaid
                or CommitteeDecisionStatuses.FullyPaid => ItemNav(AssistanceItemStatuses.Approved),
            CommitteeDecisionStatuses.Rejected => ItemNav(AssistanceItemStatuses.Rejected),
            _ => DecisionNav(status: status)
        };

    private static HomeNavigationTargetDto? ResolvePaymentActivityNavigation(string status) =>
        status switch
        {
            PaymentExecutionStatuses.AwaitingPayment or PaymentExecutionStatuses.WaitingForReference =>
                PaymentNav("finance_waiting_for_reference"),
            PaymentExecutionStatuses.Executing => PaymentNav("finance_executing"),
            PaymentExecutionStatuses.ProofUploaded => PaymentNav("finance_proof_uploaded"),
            PaymentExecutionStatuses.Paid => ItemNav(AssistanceItemStatuses.Paid),
            PaymentExecutionStatuses.ReturnedToCoordinator => PaymentNav("finance_returned"),
            PaymentExecutionStatuses.OnHold => PaymentNav("finance_on_hold"),
            _ => null
        };

    private static bool CanShowRecentActivity(AuthorizationContext auth) =>
        HasCommitteeView(auth)
        || auth.FullOrgAccess
        || auth.HasGrant(PermissionKeys.PaymentsView);

    private static HomeNavigationTargetDto WithMinAge(HomeNavigationTargetDto nav, int days) =>
        new()
        {
            TargetTab = nav.TargetTab,
            Section = nav.Section,
            Status = nav.Status,
            Ownership = nav.Ownership,
            MinAgeDays = days,
            ListView = nav.ListView
        };

    private static string FormatHebrewMonthLabel(DateTime monthStart)
    {
        var monthName = HebrewCulture.DateTimeFormat.GetAbbreviatedMonthName(monthStart.Month);
        return $"{monthName} {monthStart.Year}";
    }

    private static bool CanShowFinancialSummary(AuthorizationContext auth) =>
        auth.FullOrgAccess
        || auth.HasGrant(PermissionKeys.PaymentsView)
        || auth.HasGrant(PermissionKeys.CommitteeDecisionsView);

    private static HomeNavigationTargetDto ItemNav(string status, int? minAgeDays = null) =>
        new()
        {
            TargetTab = "decisions",
            Status = status,
            MinAgeDays = minAgeDays,
            ListView = ListViewAssistanceItems
        };

    private static HomeNavigationTargetDto DecisionNav(
        string? section = null,
        string? status = null,
        int? minAgeDays = null,
        string? listView = null,
        string? ownership = null) =>
        new()
        {
            TargetTab = "decisions",
            Section = section,
            Status = status,
            Ownership = ownership,
            MinAgeDays = minAgeDays,
            ListView = listView ?? ListViewDraftDecisions
        };

    private static HomeNavigationTargetDto PaymentNav(string section, int? minAgeDays = null) =>
        new()
        {
            TargetTab = "payments",
            Section = section,
            MinAgeDays = minAgeDays
        };

    private static bool CanShowDraftsKpi(AuthorizationContext auth) =>
        HasCommitteeView(auth)
        && (auth.FullOrgAccess
            || auth.HasGrant(PermissionKeys.CommitteeDecisionsCreate)
            || auth.HasGrant(PermissionKeys.CommitteeDecisionsEditDraft));

    private static bool CanShowCommitteeStatusKpi(AuthorizationContext auth) =>
        HasCommitteeView(auth);

    private static bool CanShowAwaitingExecutionKpi(AuthorizationContext auth) =>
        PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCreate)
        || PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExecute)
        || auth.FullOrgAccess
        || auth.HasGrant(PermissionKeys.PaymentsView);

    private static bool HasCommitteeView(AuthorizationContext auth) =>
        auth.FullOrgAccess || auth.HasGrant(PermissionKeys.CommitteeDecisionsView);

    private static bool UsesMyRecordsCommitteeScope(AuthorizationContext auth)
    {
        if (auth.FullOrgAccess)
            return false;

        var grant = auth.GetGrant(PermissionKeys.CommitteeDecisionsView);
        return grant?.Scope == PermissionScopes.MyRecords;
    }

    private static bool HasHomeDashboardAccess(AuthorizationContext auth) =>
        auth.FullOrgAccess
        || auth.HasGrant(PermissionKeys.CommitteeDecisionsView)
        || auth.HasGrant(PermissionKeys.PaymentsView)
        || auth.HasGrant(PermissionKeys.FamiliesView);

    private static JsonElement SerializeData<T>(T data) =>
        JsonSerializer.SerializeToElement(data, JsonOptions);
}
