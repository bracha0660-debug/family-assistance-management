using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class WorkflowDashboardService(
    AppDbContext db,
    CommitteeDecisionService committeeDecisionService,
    PaymentService paymentService,
    HomeWidgetComposer homeWidgetComposer)
{
    private const int PreviewLimit = 5;

    public async Task<WorkflowDashboardResponse> GetDashboardAsync(
        Guid organizationId,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var sections = new List<WorkflowSectionSummaryDto>();
        var awaitingSections = new List<WorkflowSectionCountDto>();
        var totalAwaiting = 0;

        var scopedDecisions = await LoadScopedDecisionsAsync(organizationId, auth, cancellationToken);
        var allPayments = await LoadAllPaymentsAsync(organizationId, cancellationToken);

        foreach (var section in WorkflowSectionRegistry.VisibleDecisionSections(auth))
        {
            var matching = scopedDecisions
                .Where(d => WorkflowSectionRegistry.MatchesDecisionSection(d, section.SectionId, auth.UserId))
                .ToList();
            var awaiting = WorkflowSectionRegistry.CountActionableDecisions(matching, section, auth);
            totalAwaiting += awaiting;
            if (awaiting > 0)
            {
                awaitingSections.Add(new WorkflowSectionCountDto
                {
                    SectionId = section.SectionId,
                    Title = section.Title,
                    Count = awaiting
                });
            }

            sections.Add(new WorkflowSectionSummaryDto
            {
                SectionId = section.SectionId,
                Title = section.Title,
                Visibility = section.Visibility,
                Count = matching.Count,
                AwaitingActionCount = awaiting,
                DecisionPreview = matching
                    .OrderByDescending(d => d.UpdatedAt)
                    .Take(PreviewLimit)
                    .Select(d => committeeDecisionService.MapDecisionForDashboard(d, auth))
                    .ToList()
            });
        }

        foreach (var section in WorkflowSectionRegistry.VisiblePaymentSections(auth))
        {
            var matching = allPayments
                .Where(p => WorkflowSectionRegistry.MatchesPaymentSection(p, section.SectionId))
                .ToList();
            var awaiting = WorkflowSectionRegistry.CountActionablePayments(matching, section, auth);
            totalAwaiting += awaiting;
            if (awaiting > 0)
            {
                awaitingSections.Add(new WorkflowSectionCountDto
                {
                    SectionId = section.SectionId,
                    Title = section.Title,
                    Count = awaiting
                });
            }

            sections.Add(new WorkflowSectionSummaryDto
            {
                SectionId = section.SectionId,
                Title = section.Title,
                Visibility = section.Visibility,
                Count = matching.Count,
                AwaitingActionCount = awaiting,
                PaymentPreview = matching
                    .OrderBy(p => p.CreatedAt)
                    .Take(PreviewLimit)
                    .Select(p => paymentService.MapPaymentForDashboard(p, auth))
                    .ToList()
            });
        }

        var scopedPayments = FilterPaymentsToScope(allPayments, scopedDecisions);
        var scopedActivityLogs = await LoadScopedActivityLogsAsync(
            organizationId,
            auth,
            scopedDecisions,
            scopedPayments,
            cancellationToken);

        return new WorkflowDashboardResponse
        {
            AwaitingMyAction = new AwaitingMyActionSummaryDto
            {
                TotalAwaitingMyAction = totalAwaiting,
                BySection = awaitingSections
            },
            Sections = sections,
            Home = homeWidgetComposer.Compose(auth, scopedDecisions, scopedPayments, scopedActivityLogs)
        };
    }

    private async Task<List<CommitteeDecision>> LoadScopedDecisionsAsync(
        Guid organizationId,
        AuthorizationContext auth,
        CancellationToken cancellationToken)
    {
        var query = ScopeEvaluator.ApplyCommitteeListScope(
            db.CommitteeDecisions
                .Include(d => d.Family)
                .Include(d => d.CreatedByUser)
                .Include(d => d.Items)
                .Where(d => d.OrganizationId == organizationId),
            auth,
            PermissionKeys.CommitteeDecisionsView);

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<PaymentExecution>> LoadAllPaymentsAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await db.PaymentExecutions
            .Include(p => p.AssistanceItem)
                .ThenInclude(i => i!.AssistanceType)
            .Include(p => p.AssistanceItem)
                .ThenInclude(i => i!.Supplier)
            .Include(p => p.CommitteeDecision)
                .ThenInclude(d => d!.Family)
            .Where(p => p.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

    private static List<PaymentExecution> FilterPaymentsToScope(
        IReadOnlyList<PaymentExecution> payments,
        IReadOnlyList<CommitteeDecision> scopedDecisions)
    {
        var scopedDecisionIds = scopedDecisions.Select(d => d.Id).ToHashSet();
        return payments
            .Where(p => scopedDecisionIds.Contains(p.CommitteeDecisionId))
            .ToList();
    }

    private async Task<List<AuditLog>> LoadScopedActivityLogsAsync(
        Guid organizationId,
        AuthorizationContext auth,
        IReadOnlyList<CommitteeDecision> scopedDecisions,
        IReadOnlyList<PaymentExecution> scopedPayments,
        CancellationToken cancellationToken)
    {
        var hasCommitteeView = auth.FullOrgAccess || auth.HasGrant(PermissionKeys.CommitteeDecisionsView);
        var hasPaymentsView = auth.FullOrgAccess || auth.HasGrant(PermissionKeys.PaymentsView);
        if (!hasCommitteeView && !hasPaymentsView)
            return [];

        var decisionIds = scopedDecisions.Select(d => d.Id).ToHashSet();
        var paymentIds = scopedPayments.Select(p => p.Id).ToHashSet();
        if ((!hasCommitteeView || decisionIds.Count == 0) && (!hasPaymentsView || paymentIds.Count == 0))
            return [];

        return await db.AuditLogs
            .Include(a => a.ActorUser)
            .Where(a => a.OrganizationId == organizationId
                && ((hasCommitteeView
                        && a.EntityType == "committee_decision"
                        && decisionIds.Contains(a.EntityId))
                    || (hasPaymentsView
                        && a.EntityType == "payment_execution"
                        && paymentIds.Contains(a.EntityId))))
            .OrderByDescending(a => a.CreatedAt)
            .Take(HomeWidgetComposer.RecentActivityQueryLimit)
            .ToListAsync(cancellationToken);
    }
}
