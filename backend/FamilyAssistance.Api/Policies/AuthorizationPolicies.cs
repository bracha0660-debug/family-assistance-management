using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;

namespace FamilyAssistance.Api.Policies;

public static class AuthorizationPolicies
{
    public const string SuperAdminOnly = "SuperAdminOnly";
    public const string OrgAdminOnly = "OrgAdminOnly";
    public const string CoordinatorOnly = "CoordinatorOnly";
    public const string ManagerOnly = "ManagerOnly";
    public const string FinanceOnly = "FinanceOnly";
    public const string OrgUser = "OrgUser";

    public static void AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(SuperAdminOnly, p => p.RequireAssertion(ctx =>
                ctx.User.FindFirst("role")?.Value == Roles.SuperAdmin));
            options.AddPolicy(OrgAdminOnly, p => p.RequireAssertion(ctx =>
                ctx.User.FindFirst("role")?.Value == Roles.OrganizationAdministrator));
            options.AddPolicy(CoordinatorOnly, p => p.RequireAssertion(ctx =>
                ctx.User.FindFirst("role")?.Value == Roles.Coordinator));
            options.AddPolicy(ManagerOnly, p => p.RequireAssertion(ctx =>
                ctx.User.FindFirst("role")?.Value == Roles.Manager));
            options.AddPolicy(FinanceOnly, p => p.RequireAssertion(ctx =>
                ctx.User.FindFirst("role")?.Value == Roles.Finance));
            options.AddPolicy(OrgUser, p => p.RequireAssertion(ctx =>
            {
                var role = ctx.User.FindFirst("role")?.Value;
                return role is not null && role != Roles.SuperAdmin;
            }));
        });
    }
}

public static class SessionAuthorizationExtensions
{
    public static RouteHandlerBuilder RequireAuthorization(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            if (httpContext.GetCurrentUser() is null)
            {
                return Results.Json(
                    new Models.ApiError { Error = "לא מחובר", Code = "UNAUTHORIZED" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireSuperAdmin(this RouteHandlerBuilder builder)
    {
        return builder.RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var currentUser = httpContext.GetCurrentUser();
            if (currentUser?.Role != Roles.SuperAdmin)
            {
                return Results.Json(
                    new Models.ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireOrgAdmin(this RouteHandlerBuilder builder)
    {
        return builder.RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var currentUser = httpContext.GetCurrentUser();
            if (currentUser?.Role != Roles.OrganizationAdministrator || currentUser.OrganizationId is null)
            {
                return Results.Json(
                    new Models.ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
    }
}
