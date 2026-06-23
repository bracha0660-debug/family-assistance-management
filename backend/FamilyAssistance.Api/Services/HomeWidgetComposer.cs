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

    public HomeDashboardDto Compose(
        AuthorizationContext auth,
        IReadOnlyList<CommitteeDecision> scopedDecisions,
        IReadOnlyList<PaymentExecution> scopedPayments)
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

    private static HomeNavigationTargetDto DecisionNav(string? section = null, string? status = null) =>
        new()
        {
            TargetTab = "decisions",
            Section = section,
            Status = status
        };

    private static HomeNavigationTargetDto PaymentNav(string section) =>
        new()
        {
            TargetTab = "payments",
            Section = section
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
