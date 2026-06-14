using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class OrganizationActivityService(AppDbContext db)
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;

    public async Task<ServiceResult<ActivityLogListResponse>> ListActivityAsync(
        Guid organizationId,
        int? limit,
        int? offset,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit)
            return ServiceResult<ActivityLogListResponse>.Fail(400, "VALIDATION_ERROR",
                $"limit חייב להיות בין 1 ל-{MaxLimit}");

        var effectiveOffset = offset ?? 0;
        if (effectiveOffset < 0)
            return ServiceResult<ActivityLogListResponse>.Fail(400, "VALIDATION_ERROR",
                "offset חייב להיות 0 או גדול יותר");

        var entries = await db.AuditLogs
            .Where(a => a.OrganizationId == organizationId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .Join(
                db.Users,
                a => a.ActorUserId,
                u => u.Id,
                (a, u) => new ActivityLogEntryDto
                {
                    Id = a.Id,
                    CreatedAt = a.CreatedAt,
                    EventCode = a.EventCode,
                    ActorUserId = a.ActorUserId,
                    ActorUsername = u.Username,
                    ActorFullName = u.FullName,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    Action = a.Action,
                    FieldName = a.FieldName,
                    OldValue = a.OldValue,
                    NewValue = a.NewValue,
                    Reason = a.Reason
                })
            .ToListAsync(cancellationToken);

        return ServiceResult<ActivityLogListResponse>.Ok(new ActivityLogListResponse
        {
            Entries = entries,
            ReturnedCount = entries.Count
        });
    }
}
