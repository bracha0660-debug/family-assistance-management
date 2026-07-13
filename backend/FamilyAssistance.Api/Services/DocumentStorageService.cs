namespace FamilyAssistance.Api.Services;

public sealed class DocumentStorageService(IConfiguration configuration, ILogger<DocumentStorageService> logger)
{
    private readonly string _uploadRoot = configuration["Storage:UploadPath"] ?? "uploads";

    public async Task<(string StoredFileName, string FullPath)> SaveAsync(
        Guid organizationId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var orgDir = Path.Combine(_uploadRoot, organizationId.ToString("D"));
        Directory.CreateDirectory(orgDir);

        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(orgDir, storedFileName);

        await using var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);

        logger.LogInformation("Saved upload {StoredFileName} for org {OrganizationId}", storedFileName, organizationId);
        return (storedFileName, fullPath);
    }

    public Task<(Stream Content, string ContentType)?> OpenReadAsync(
        Guid organizationId,
        string storedFileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (storedFileName.Contains("..", StringComparison.Ordinal) || storedFileName.Contains('/', StringComparison.Ordinal)
            || storedFileName.Contains('\\', StringComparison.Ordinal))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        var fullPath = Path.Combine(_uploadRoot, organizationId.ToString("D"), storedFileName);
        if (!File.Exists(fullPath))
            return Task.FromResult<(Stream, string)?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<(Stream, string)?>((stream, contentType ?? "application/octet-stream"));
    }

    public bool Delete(Guid organizationId, string storedFileName)
    {
        if (storedFileName.Contains("..", StringComparison.Ordinal))
            return false;

        var fullPath = Path.Combine(_uploadRoot, organizationId.ToString("D"), storedFileName);
        if (!File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        return true;
    }
}
