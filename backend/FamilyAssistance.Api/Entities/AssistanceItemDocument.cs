namespace FamilyAssistance.Api.Entities;

public class AssistanceItemDocument
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AssistanceItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public AssistanceItem? AssistanceItem { get; set; }
    public User? UploadedByUser { get; set; }
}
