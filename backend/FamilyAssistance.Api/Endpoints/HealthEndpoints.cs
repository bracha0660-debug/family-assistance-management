using FamilyAssistance.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/health", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            try
            {
                var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    return Results.Json(
                        new { status = "unhealthy", database = "disconnected" },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                return Results.Ok(new { status = "healthy", database = "connected" });
            }
            catch
            {
                return Results.Json(
                    new { status = "unhealthy", database = "disconnected" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }
}
