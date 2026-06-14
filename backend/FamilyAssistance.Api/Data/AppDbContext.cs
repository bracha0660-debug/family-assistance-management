using FamilyAssistance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankAccountHistory> BankAccountHistory => Set<BankAccountHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<AssistanceType> AssistanceTypes => Set<AssistanceType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        ApplySnakeCaseNames(modelBuilder);

        modelBuilder.Entity<Organization>(e =>
        {
            e.ToTable("organizations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_organizations_code");
            e.HasIndex(x => x.Status).HasDatabaseName("ix_organizations_status");
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Code).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => x.Username).IsUnique().HasDatabaseName("ux_users_username");
            e.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_users_org_status");
            e.HasIndex(x => new { x.OrganizationId, x.Role }).HasDatabaseName("ix_users_org_role");
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne(x => x.Organization).WithMany(x => x.Users).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<UserSession>(e =>
        {
            e.ToTable("user_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => x.SessionTokenHash).IsUnique().HasDatabaseName("ux_sessions_token_hash");
            e.HasIndex(x => new { x.UserId, x.RevokedAt, x.ExpiresAt }).HasDatabaseName("ix_sessions_user_active");
            e.Property(x => x.SessionTokenHash).HasMaxLength(128);
            e.HasOne(x => x.User).WithMany(x => x.Sessions).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<BankAccount>(e =>
        {
            e.ToTable("bank_accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.OwnerEntityType).HasMaxLength(50);
            e.Property(x => x.BankNumber).HasMaxLength(10);
            e.Property(x => x.BranchNumber).HasMaxLength(10);
            e.Property(x => x.AccountNumber).HasMaxLength(34);
            e.HasIndex(x => new { x.OrganizationId, x.OwnerEntityType, x.OwnerEntityId })
                .IsUnique()
                .HasFilter("is_active = true")
                .HasDatabaseName("ux_bank_accounts_active_owner");
            e.HasIndex(x => new { x.OrganizationId, x.BankNumber, x.BranchNumber, x.AccountNumber })
                .IsUnique()
                .HasFilter("is_active = true")
                .HasDatabaseName("ux_bank_accounts_org_full_identity");
            e.HasOne(x => x.Organization).WithMany(x => x.BankAccounts).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<BankAccountHistory>(e =>
        {
            e.ToTable("bank_account_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ChangeType).HasMaxLength(30);
            e.Property(x => x.BankNumber).HasMaxLength(10);
            e.Property(x => x.BranchNumber).HasMaxLength(10);
            e.Property(x => x.AccountNumber).HasMaxLength(34);
            e.HasIndex(x => new { x.BankAccountId, x.CreatedAt }).HasDatabaseName("ix_bank_history_account");
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt }).HasDatabaseName("ix_bank_history_org");
            e.HasOne(x => x.BankAccount).WithMany(x => x.History).HasForeignKey(x => x.BankAccountId);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.EventCode).HasMaxLength(10);
            e.Property(x => x.EntityType).HasMaxLength(50);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.FieldName).HasMaxLength(100);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt }).HasDatabaseName("ix_audit_org_time");
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt }).HasDatabaseName("ix_audit_entity");
            e.HasIndex(x => new { x.ActorUserId, x.CreatedAt }).HasDatabaseName("ix_audit_actor");
            e.HasIndex(x => new { x.EventCode, x.CreatedAt }).HasDatabaseName("ix_audit_event_code");
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId);
        });

        modelBuilder.Entity<SecurityAuditLog>(e =>
        {
            e.ToTable("security_audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.EventCode).HasMaxLength(10);
            e.Property(x => x.EventType).HasMaxLength(50);
            e.Property(x => x.UsernameAttempted).HasMaxLength(100);
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_security_audit_time");
            e.HasIndex(x => new { x.UsernameAttempted, x.CreatedAt }).HasDatabaseName("ix_security_audit_username");
            e.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_security_audit_user");
            e.HasIndex(x => new { x.EventCode, x.CreatedAt }).HasDatabaseName("ix_security_audit_event");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
        });

        modelBuilder.Entity<Family>(e =>
        {
            e.ToTable("families");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.FamilyCode).HasMaxLength(20);
            e.Property(x => x.HeadOfHouseholdName).HasMaxLength(200);
            e.Property(x => x.HeadIdNumber).HasMaxLength(9);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => new { x.OrganizationId, x.FamilyCode })
                .IsUnique()
                .HasDatabaseName("ux_families_org_code");
            e.HasIndex(x => new { x.OrganizationId, x.Status })
                .HasDatabaseName("ix_families_org_status");
            e.HasIndex(x => new { x.OrganizationId, x.AssignedCoordinatorId, x.Status })
                .HasDatabaseName("ix_families_org_coordinator_status");
            e.HasOne(x => x.Organization).WithMany(x => x.Families).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.AssignedCoordinator).WithMany().HasForeignKey(x => x.AssignedCoordinatorId);
        });

        modelBuilder.Entity<AssistanceType>(e =>
        {
            e.ToTable("assistance_types");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TypeCode).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Frequency).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.DefaultAmount).HasColumnType("numeric(14,2)");
            e.HasIndex(x => new { x.OrganizationId, x.TypeCode })
                .IsUnique()
                .HasDatabaseName("ux_assistance_types_org_code");
            e.HasIndex(x => new { x.OrganizationId, x.Status })
                .HasDatabaseName("ix_assistance_types_org_status");
            e.HasOne(x => x.Organization).WithMany(x => x.AssistanceTypes).HasForeignKey(x => x.OrganizationId);
        });
    }

    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
