using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Endpoints;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FamSessionOptions>(builder.Configuration.GetSection("Session"));
builder.Services.AddSingleton(sp =>
{
    var options = new FamSessionOptions();
    builder.Configuration.GetSection("Session").Bind(options);
    return options;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

builder.Services.AddSingleton<LoginRateLimiter>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<ISecurityAuditService, SecurityAuditService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<OrganizationPermissionService>();
builder.Services.AddScoped<OrganizationRoleService>();
builder.Services.AddScoped<OrganizationAdminService>();
builder.Services.AddScoped<UserDtoBuilder>();
builder.Services.AddScoped<OrganizationUserService>();
builder.Services.AddScoped<UserPermissionOverrideService>();
builder.Services.AddScoped<OrganizationActivityService>();
builder.Services.AddScoped<FamilyService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<CommitteeDecisionService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<WorkflowDashboardService>();
builder.Services.AddSingleton<DocumentStorageService>();
builder.Services.AddScoped<AssistanceTypeService>();
builder.Services.AddAuthorizationPolicies();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var permissionService = scope.ServiceProvider.GetRequiredService<PermissionService>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(db, permissionService, config, logger);
}

app.UseCors();
app.UseSessionAuth();
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapAdminOrganizationsEndpoints();
app.MapOrgUsersEndpoints();
app.MapOrgActivityEndpoints();
app.MapOrgPermissionsEndpoints();
app.MapOrgRolesEndpoints();
app.MapFamiliesEndpoints();
app.MapSuppliersEndpoints();
app.MapCommitteeDecisionsEndpoints();
app.MapPaymentsEndpoints();
app.MapWorkflowEndpoints();
app.MapAssistanceTypesEndpoints();

app.Run();
