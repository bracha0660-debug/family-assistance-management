namespace FamilyAssistance.Api.Models;

public sealed class WorkflowDashboardResponse
{
    public required AwaitingMyActionSummaryDto AwaitingMyAction { get; init; }
    public required IReadOnlyList<WorkflowSectionSummaryDto> Sections { get; init; }
}

public sealed class AwaitingMyActionSummaryDto
{
    public int TotalAwaitingMyAction { get; init; }
    public required IReadOnlyList<WorkflowSectionCountDto> BySection { get; init; }
}

public sealed class WorkflowSectionCountDto
{
    public string SectionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class WorkflowSectionSummaryDto
{
    public string SectionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Visibility { get; init; } = string.Empty;
    public int Count { get; init; }
    public int AwaitingActionCount { get; init; }
    public IReadOnlyList<CommitteeDecisionDto>? DecisionPreview { get; init; }
    public IReadOnlyList<PaymentQueueItemDto>? PaymentPreview { get; init; }
}
