namespace FamilyAssistance.Api.Entities;

public class PermissionCatalog
{
    public string PermissionKey { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayNameHe { get; set; } = string.Empty;
    public string? DescriptionHe { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool SupportsMyRecords { get; set; }
    public bool ScopeApplies { get; set; } = true;
}
