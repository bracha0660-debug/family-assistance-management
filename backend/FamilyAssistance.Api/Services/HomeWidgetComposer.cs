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

        var kpiCards = BuildKpiCards(auth, scopedDecisions, scopedPayments);
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

        var financialMetrics = BuildFinancialSummary(auth, scopedDecisions, scopedPayments);
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

        var bottlenecks = BuildBottlenecks(auth, scopedDecisions, scopedPayments);
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

        var monthlyTrend = BuildMonthlyTrend(auth, scopedDecisions);
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
        IReadOnlyList<PaymentExecution> scopedPayments)
    {
        var cards = new List<HomeKpiCardDto>();
        var mineScope = UsesMyRecordsCommitteeScope(auth);
        var hasApprove = auth.FullOrgAccess || auth.HasGrant(PermissionKeys.CommitteeDecisionsApprove);

        if (CanShowDraftsKpi(auth))
        {
            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "drafts",
                Title = "טיוטות",
                Subtitle = "החלטות בטיוטה",
                Count = scopedDecisions.Count(d => d.Status == CommitteeDecisionStatuses.Draft),
                StatusSemantic = HomeWorkflowStatus.Draft,
                NavigationTarget = mineScope
                    ? DecisionNav("my_drafts")
                    : DecisionNav(status: CommitteeDecisionStatuses.Draft)
            });
        }

        if (CanShowCommitteeStatusKpi(auth))
        {
            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "awaiting_approval",
                Title = "ממתין לאישור",
                Subtitle = "החלטות שהוגשו",
                Count = scopedDecisions.Count(d => d.Status == CommitteeDecisionStatuses.Submitted),
                StatusSemantic = HomeWorkflowStatus.PendingApproval,
                NavigationTarget = ResolveSubmittedNavigation(mineScope, hasApprove)
            });

            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "returned_for_revision",
                Title = "הוחזר לטיפול",
                Subtitle = "הוחזרו להשלמות",
                Count = scopedDecisions.Count(d => d.Status == CommitteeDecisionStatuses.ReturnedForRevision),
                StatusSemantic = HomeWorkflowStatus.ReturnedForTreatment,
                NavigationTarget = ResolveReturnedNavigation(mineScope, hasApprove)
            });

            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "suspended",
                Title = "בהשהיה",
                Subtitle = "החלטות בהשהיה",
                Count = scopedDecisions.Count(d => d.Status == CommitteeDecisionStatuses.Suspended),
                StatusSemantic = HomeWorkflowStatus.OnHold,
                NavigationTarget = ResolveSuspendedNavigation(mineScope, hasApprove)
            });
        }

        if (CanShowAwaitingExecutionKpi(auth))
        {
            var awaitingCount = scopedPayments.Count(p =>
                WorkflowSectionRegistry.MatchesPaymentSection(p, "finance_awaiting_execution"));

            cards.Add(new HomeKpiCardDto
            {
                KpiKey = "awaiting_execution",
                Title = "ממתין לביצוע",
                Subtitle = "תשלומים ממתינים לביצוע",
                Count = awaitingCount,
                StatusSemantic = HomeWorkflowStatus.PendingExecution,
                NavigationTarget = PaymentNav("finance_awaiting_execution")
            });
        }

        return cards;
    }

    private static List<HomeFinancialMetricDto> BuildFinancialSummary(
        AuthorizationContext auth,
        IReadOnlyList<CommitteeDecision> scopedDecisions,
        IReadOnlyList<PaymentExecution> scopedPayments)
    {
        if (!CanShowFinancialSummary(auth))
            return [];

        var mineScope = UsesMyRecordsFinancialScope(auth);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var approvedThisMonth = scopedDecisions
            .Where(d => d.ApprovedAt is not null
                && d.ApprovedAt.Value >= monthStart
                && d.ApprovedAt.Value < monthEnd)
            .Sum(d => d.TotalAmount);

        var paidThisMonth = scopedPayments
            .Where(p => p.PaidAt is not null
                && p.PaidAt.Value >= monthStart
                && p.PaidAt.Value < monthEnd)
            .Sum(p => p.AssistanceItem?.Amount ?? 0m);

        var awaitingExecution = scopedPayments
            .Where(p => WorkflowSectionRegistry.MatchesPaymentSection(p, "finance_awaiting_execution"))
            .Sum(p => p.AssistanceItem?.Amount ?? 0m);

        var suspendedDecisions = scopedDecisions
            .Where(d => d.Status == CommitteeDecisionStatuses.Suspended)
            .Sum(d => d.TotalAmount);

        var onHoldPayments = scopedPayments
            .Where(p => p.Status == PaymentExecutionStatuses.OnHold
                && p.CommitteeDecision?.Status != CommitteeDecisionStatuses.Suspended)
            .Sum(p => p.AssistanceItem?.Amount ?? 0m);

        var onHoldTotal = suspendedDecisions + onHoldPayments;

        return
        [
            new HomeFinancialMetricDto
            {
                MetricKey = "approved_this_month",
                Title = "אושר החודש",
                Amount = approvedThisMonth,
                StatusSemantic = HomeWorkflowStatus.Paid,
                NavigationTarget = ResolveApprovedThisMonthNavigation(mineScope)
            },
            new HomeFinancialMetricDto
            {
                MetricKey = "paid_this_month",
                Title = "שולם החודש",
                Amount = paidThisMonth,
                StatusSemantic = HomeWorkflowStatus.Paid,
                NavigationTarget = PaymentNav("finance_paid")
            },
            new HomeFinancialMetricDto
            {
                MetricKey = "awaiting_execution",
                Title = "ממתין לביצוע",
                Amount = awaitingExecution,
                StatusSemantic = HomeWorkflowStatus.PendingExecution,
                NavigationTarget = PaymentNav("finance_awaiting_execution")
            },
            new HomeFinancialMetricDto
            {
                MetricKey = "suspended",
                Title = "בהשהיה",
                Amount = onHoldTotal,
                StatusSemantic = HomeWorkflowStatus.OnHold,
                NavigationTarget = ResolveOnHoldNavigation(mineScope)
            }
        ];
    }

    private static HomeMonthlyTrendDataDto? BuildMonthlyTrend(
        AuthorizationContext auth,
        IReadOnlyList<CommitteeDecision> scopedDecisions)
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
            var amount = scopedDecisions
                .Where(d => d.ApprovedAt is not null
                    && d.ApprovedAt.Value >= monthStart
                    && d.ApprovedAt.Value < monthEnd)
                .Sum(d => d.TotalAmount);

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
        IReadOnlyList<CommitteeDecision> scopedDecisions,
        IReadOnlyList<PaymentExecution> scopedPayments)
    {
        var alerts = new List<HomeBottleneckAlertDto>();
        var now = DateTime.UtcNow;
        var mineScope = UsesMyRecordsCommitteeScope(auth);
        var hasApprove = auth.FullOrgAccess || auth.HasGrant(PermissionKeys.CommitteeDecisionsApprove);

        if (HasCommitteeView(auth))
        {
            var submittedCutoff = now.AddDays(-StaleSubmittedDays);
            alerts.Add(new HomeBottleneckAlertDto
            {
                AlertKey = "stale_submitted",
                Title = "החלטות ממתינות מעל 7 ימים",
                Description = "החלטות שהוגשו וממתינות לאישור מעל 7 ימים",
                Count = scopedDecisions.Count(d =>
                    d.Status == CommitteeDecisionStatuses.Submitted
                    && d.SubmittedAt is not null
                    && d.SubmittedAt < submittedCutoff),
                ThresholdDays = StaleSubmittedDays,
                StatusSemantic = HomeWorkflowStatus.PendingApproval,
                NavigationTarget = WithMinAge(ResolveSubmittedNavigation(mineScope, hasApprove), StaleSubmittedDays)
            });

            var suspendedCutoff = now.AddDays(-StaleSuspendedDays);
            alerts.Add(new HomeBottleneckAlertDto
            {
                AlertKey = "stale_suspended",
                Title = "החלטות בהשהיה מעל 30 יום",
                Description = "החלטות בהשהיה מעל 30 יום",
                Count = scopedDecisions.Count(d =>
                    d.Status == CommitteeDecisionStatuses.Suspended
                    && d.SuspendedAt is not null
                    && d.SuspendedAt < suspendedCutoff),
                ThresholdDays = StaleSuspendedDays,
                StatusSemantic = HomeWorkflowStatus.OnHold,
                NavigationTarget = WithMinAge(ResolveSuspendedNavigation(mineScope, hasApprove), StaleSuspendedDays)
            });
        }

        if (auth.FullOrgAccess || auth.HasGrant(PermissionKeys.PaymentsView))
        {
            var paymentCutoff = now.AddDays(-StaleAwaitingPaymentDays);
            alerts.Add(new HomeBottleneckAlertDto
            {
                AlertKey = "stale_awaiting_payment",
                Title = "תשלומים ממתינים לביצוע מעל 14 יום",
                Description = "תשלומים ממתינים לביצוע מעל 14 יום",
                Count = scopedPayments.Count(p =>
                    WorkflowSectionRegistry.MatchesPaymentSection(p, "finance_awaiting_execution")
                    && p.CreatedAt < paymentCutoff),
                ThresholdDays = StaleAwaitingPaymentDays,
                StatusSemantic = HomeWorkflowStatus.PendingExecution,
                NavigationTarget = PaymentNav("finance_awaiting_execution", StaleAwaitingPaymentDays)
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
            CommitteeDecisionStatuses.Draft => mineScope
                ? DecisionNav("my_drafts")
                : DecisionNav(status: CommitteeDecisionStatuses.Draft),
            CommitteeDecisionStatuses.Submitted => ResolveSubmittedNavigation(mineScope, hasApprove),
            CommitteeDecisionStatuses.ReturnedForRevision => ResolveReturnedNavigation(mineScope, hasApprove),
            CommitteeDecisionStatuses.Suspended => ResolveSuspendedNavigation(mineScope, hasApprove),
            CommitteeDecisionStatuses.Approved or CommitteeDecisionStatuses.PartiallyPaid
                or CommitteeDecisionStatuses.FullyPaid => mineScope
                    ? DecisionNav("my_in_finance_execution")
                    : DecisionNav("approved"),
            CommitteeDecisionStatuses.Rejected => DecisionNav(status: CommitteeDecisionStatuses.Rejected),
            CommitteeDecisionStatuses.Cancelled => DecisionNav(status: CommitteeDecisionStatuses.Cancelled),
            _ => DecisionNav(status: status)
        };

    private static HomeNavigationTargetDto? ResolvePaymentActivityNavigation(string status) =>
        status switch
        {
            PaymentExecutionStatuses.AwaitingPayment => PaymentNav("finance_awaiting_execution"),
            PaymentExecutionStatuses.Executing => PaymentNav("finance_executing"),
            PaymentExecutionStatuses.ProofUploaded => PaymentNav("finance_proof_uploaded"),
            PaymentExecutionStatuses.Paid => PaymentNav("finance_paid"),
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
            MinAgeDays = days
        };

    private static string FormatHebrewMonthLabel(DateTime monthStart)
    {
        var monthName = HebrewCulture.DateTimeFormat.GetAbbreviatedMonthName(monthStart.Month);
        return $"{monthName} {monthStart.Year}";
    }

    private static HomeNavigationTargetDto ResolveApprovedThisMonthNavigation(bool mineScope) =>
        mineScope
            ? DecisionNav("my_in_finance_execution")
            : DecisionNav("approved");

    private static HomeNavigationTargetDto ResolveOnHoldNavigation(bool mineScope) =>
        mineScope
            ? DecisionNav("my_suspended")
            : PaymentNav("finance_on_hold");

    private static bool CanShowFinancialSummary(AuthorizationContext auth) =>
        auth.FullOrgAccess
        || auth.HasGrant(PermissionKeys.PaymentsView)
        || auth.HasGrant(PermissionKeys.CommitteeDecisionsView);

    private static bool UsesMyRecordsFinancialScope(AuthorizationContext auth)
    {
        if (auth.FullOrgAccess)
            return false;

        var committeeGrant = auth.GetGrant(PermissionKeys.CommitteeDecisionsView);
        if (committeeGrant?.Scope == PermissionScopes.MyRecords)
            return true;

        var paymentsGrant = auth.GetGrant(PermissionKeys.PaymentsView);
        return paymentsGrant?.Scope == PermissionScopes.MyRecords;
    }

    private static HomeNavigationTargetDto ResolveSubmittedNavigation(bool mineScope, bool hasApprove) =>
        mineScope
            ? DecisionNav("my_waiting_manager_approval")
            : hasApprove
                ? DecisionNav("waiting_my_approval")
                : DecisionNav(status: CommitteeDecisionStatuses.Submitted);

    private static HomeNavigationTargetDto ResolveReturnedNavigation(bool mineScope, bool hasApprove) =>
        mineScope
            ? DecisionNav("my_returned_for_revision")
            : hasApprove
                ? DecisionNav("manager_returned")
                : DecisionNav(status: CommitteeDecisionStatuses.ReturnedForRevision);

    private static HomeNavigationTargetDto ResolveSuspendedNavigation(bool mineScope, bool hasApprove) =>
        mineScope
            ? DecisionNav("my_suspended")
            : hasApprove
                ? DecisionNav("manager_suspended")
                : DecisionNav(status: CommitteeDecisionStatuses.Suspended);

    private static HomeNavigationTargetDto DecisionNav(string? section = null, string? status = null, int? minAgeDays = null) =>
        new()
        {
            TargetTab = "decisions",
            Section = section,
            Status = status,
            MinAgeDays = minAgeDays
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
        PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExecute);

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
