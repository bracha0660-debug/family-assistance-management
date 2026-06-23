using System.Text.Json;

namespace FamilyAssistance.Api.Models;

public static class HomeWidgetTypes
{
    public const string KpiCards = "kpi_cards";
    public const string FinancialSummary = "financial_summary";
    public const string Bottlenecks = "bottlenecks";
    public const string MonthlyTrend = "monthly_trend";
    public const string RecentActivity = "recent_activity";
}

public sealed class HomeDashboardDto
{
    public required DateTime GeneratedAt { get; init; }
    public required IReadOnlyList<HomeWidgetDto> Widgets { get; init; }
}

public sealed class HomeWidgetDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public JsonElement? Data { get; init; }
    public HomeNavigationTargetDto? NavigationTarget { get; init; }
}

public sealed class HomeNavigationTargetDto
{
    public string TargetTab { get; init; } = string.Empty;
    public string? Section { get; init; }
    public string? Status { get; init; }
    public string? Ownership { get; init; }
    public int? MinAgeDays { get; init; }
}

public sealed class HomeKpiCardsDataDto
{
    public required IReadOnlyList<HomeKpiCardDto> Cards { get; init; }
}

public sealed class HomeKpiCardDto
{
    public string KpiKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public int Count { get; init; }
    public string StatusSemantic { get; init; } = string.Empty;
    public required HomeNavigationTargetDto NavigationTarget { get; init; }
}

public sealed class HomeFinancialSummaryDataDto
{
    public required IReadOnlyList<HomeFinancialMetricDto> Metrics { get; init; }
}

public sealed class HomeFinancialMetricDto
{
    public string MetricKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string StatusSemantic { get; init; } = string.Empty;
    public HomeNavigationTargetDto? NavigationTarget { get; init; }
}

public sealed class HomeMonthlyTrendDataDto
{
    public string Subtitle { get; init; } = string.Empty;
    public required IReadOnlyList<HomeMonthlyTrendPointDto> Points { get; init; }
}

public sealed class HomeMonthlyTrendPointDto
{
    public string MonthKey { get; init; } = string.Empty;
    public string LabelHe { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
