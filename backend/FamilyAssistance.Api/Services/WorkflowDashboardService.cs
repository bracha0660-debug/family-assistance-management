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
    PaymentService paymentService)
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

        return new WorkflowDashboardResponse
        {
            AwaitingMyAction = new AwaitingMyActionSummaryDto
            {
                TotalAwaitingMyAction = totalAwaiting,
                BySection = awaitingSections
            },
            Sections = sections
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
}
