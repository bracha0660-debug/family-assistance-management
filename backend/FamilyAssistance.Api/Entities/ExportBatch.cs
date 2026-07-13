namespace FamilyAssistance.Api.Entities;

/// <summary>
/// One export sheet generated from selected payment rows (Phase 16).
/// Soft-cancel only — never hard-delete batches.
/// </summary>
public class ExportBatch
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    /// <summary>Stable batch identifier (e.g. EB-000001). Never reused for a new sheet on re-download.</summary>
    public string BatchNumber { get; set; } = string.Empty;
    /// <summary>open | partially_cancelled | cancelled</summary>
    public string Status { get; set; } = "open";
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? FileName { get; set; }
    public string? StoredFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime? GeneratedAt { get; set; }

    public int TotalItemCount { get; set; }
    public int ActiveItemCount { get; set; }
    public int CancelledItemCount { get; set; }

    public Organization? Organization { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<ExportBatchItem> Items { get; set; } = [];
}
