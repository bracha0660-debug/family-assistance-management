using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Policies;

public static class AuthorizationPolicies
{
    public static void AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization();
    }
}

public static class SessionAuthorizationExtensions
{
    private const string AuthContextKey = "AuthorizationContext";

    public static AuthorizationContext? GetAuthorizationContext(this HttpContext context)
        => context.Items[AuthContextKey] as AuthorizationContext;

    public static RouteHandlerBuilder RequireAuthorization(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            if (context.HttpContext.GetCurrentUser() is null)
            {
                return Results.Json(
                    new ApiError { Error = "לא מחובר", Code = "UNAUTHORIZED" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireSuperAdmin(this RouteHandlerBuilder builder)
    {
        return builder.RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var currentUser = context.HttpContext.GetCurrentUser();
            if (currentUser?.Role != Roles.SuperAdmin)
            {
                return Results.Json(
                    new ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireOrgAdmin(this RouteHandlerBuilder builder)
    {
        return builder.RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var currentUser = context.HttpContext.GetCurrentUser();
            if (currentUser is null || !currentUser.HasOrgAdminAccess())
            {
                return Results.Json(
                    new ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireOrgContext(this RouteHandlerBuilder builder)
    {
        return builder.RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var currentUser = httpContext.GetCurrentUser()!;
            var permissionService = httpContext.RequestServices.GetRequiredService<PermissionService>();
            var auth = await permissionService.BuildAuthorizationContextAsync(currentUser, context.HttpContext.RequestAborted);

            if (auth.EffectiveOrganizationId is null)
            {
                return Results.Json(
                    new ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            httpContext.Items[AuthContextKey] = auth;
            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireWorkflowPermission(this RouteHandlerBuilder builder, string permissionKey)
    {
        return builder.RequireOrgContext().AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var auth = httpContext.GetAuthorizationContext()
                ?? await httpContext.RequestServices
                    .GetRequiredService<PermissionService>()
                    .BuildAuthorizationContextAsync(httpContext.GetCurrentUser()!, context.HttpContext.RequestAborted);

            if (!PermissionService.HasWorkflowGrant(auth, permissionKey))
            {
                return Results.Json(
                    new ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            httpContext.Items[AuthContextKey] = auth;
            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permissionKey)
    {
        return builder.RequireOrgContext().AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var auth = httpContext.GetAuthorizationContext()
                ?? await httpContext.RequestServices
                    .GetRequiredService<PermissionService>()
                    .BuildAuthorizationContextAsync(httpContext.GetCurrentUser()!, context.HttpContext.RequestAborted);

            var permissionService = httpContext.RequestServices.GetRequiredService<PermissionService>();
            if (!await permissionService.HasGrantAsync(auth, permissionKey, context.HttpContext.RequestAborted))
            {
                return Results.Json(
                    new ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            httpContext.Items[AuthContextKey] = auth;
            return await next(context);
        });
    }
}
