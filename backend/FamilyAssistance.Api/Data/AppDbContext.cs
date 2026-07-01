using FamilyAssistance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<AssistanceType> AssistanceTypes => Set<AssistanceType>();
    public DbSet<AssistanceTypeSupplier> AssistanceTypeSuppliers => Set<AssistanceTypeSupplier>();
    public DbSet<CommitteeDecision> CommitteeDecisions => Set<CommitteeDecision>();
    public DbSet<AssistanceItem> AssistanceItems => Set<AssistanceItem>();
    public DbSet<AssistanceItemDocument> AssistanceItemDocuments => Set<AssistanceItemDocument>();
    public DbSet<PaymentExecution> PaymentExecutions => Set<PaymentExecution>();
    public DbSet<PermissionCatalog> PermissionCatalog => Set<PermissionCatalog>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<OrganizationRoleGrant> OrganizationRoleGrants => Set<OrganizationRoleGrant>();
    public DbSet<OrganizationRolePermission> OrganizationRolePermissions => Set<OrganizationRolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

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
            e.Property(x => x.LogoUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => x.Username).IsUnique().HasDatabaseName("ux_users_username");
            e.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_users_org_status");
            e.HasIndex(x => new { x.OrganizationId, x.Role }).HasDatabaseName("ix_users_org_role");
            e.HasIndex(x => x.OrganizationRoleId).HasDatabaseName("ix_users_organization_role_id");
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne(x => x.Organization).WithMany(x => x.Users).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.OrganizationRole).WithMany(x => x.Users).HasForeignKey(x => x.OrganizationRoleId);
        });

        modelBuilder.Entity<UserSession>(e =>
        {
            e.ToTable("user_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => x.SessionTokenHash).IsUnique().HasDatabaseName("ux_sessions_token_hash");
            e.HasIndex(x => new { x.UserId, x.RevokedAt, x.ExpiresAt }).HasDatabaseName("ix_sessions_user_active");
            e.HasIndex(x => x.ActingOrganizationId).HasDatabaseName("ix_user_sessions_acting_org");
            e.Property(x => x.SessionTokenHash).HasMaxLength(128);
            e.HasOne(x => x.User).WithMany(x => x.Sessions).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.ActingOrganization).WithMany().HasForeignKey(x => x.ActingOrganizationId);
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
            e.Property(x => x.FamilyLastName).HasMaxLength(200);
            e.Property(x => x.FatherName).HasMaxLength(200);
            e.Property(x => x.FatherIsraeliId).HasMaxLength(9);
            e.Property(x => x.MotherName).HasMaxLength(200);
            e.Property(x => x.MotherIsraeliId).HasMaxLength(9);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.BankNumber).HasMaxLength(10);
            e.Property(x => x.BranchNumber).HasMaxLength(10);
            e.Property(x => x.AccountNumber).HasMaxLength(34);
            e.Property(x => x.AccountHolderName).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.OrganizationId, x.FamilyCode })
                .IsUnique()
                .HasDatabaseName("ux_families_org_code");
            e.HasIndex(x => new { x.OrganizationId, x.AccountingCoordinatorId, x.AccountingCode })
                .IsUnique()
                .HasDatabaseName("ux_families_org_acct_coord_code");
            e.HasIndex(x => new { x.OrganizationId, x.FatherIsraeliId })
                .HasDatabaseName("ix_families_org_father_id")
                .HasFilter("father_israeli_id IS NOT NULL");
            e.HasIndex(x => new { x.OrganizationId, x.MotherIsraeliId })
                .HasDatabaseName("ix_families_org_mother_id")
                .HasFilter("mother_israeli_id IS NOT NULL");
            e.HasIndex(x => new { x.OrganizationId, x.Status })
                .HasDatabaseName("ix_families_org_status");
            e.HasIndex(x => new { x.OrganizationId, x.AssignedCoordinatorId, x.Status })
                .HasDatabaseName("ix_families_org_coordinator_status");
            e.HasOne(x => x.Organization).WithMany(x => x.Families).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.AssignedCoordinator).WithMany().HasForeignKey(x => x.AssignedCoordinatorId);
            e.HasOne(x => x.AccountingCoordinator).WithMany().HasForeignKey(x => x.AccountingCoordinatorId);
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.SupplierCode).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.RegistrationNumber).HasMaxLength(50);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.AccountingCode).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.BankNumber).HasMaxLength(10);
            e.Property(x => x.BranchNumber).HasMaxLength(10);
            e.Property(x => x.AccountNumber).HasMaxLength(34);
            e.Property(x => x.AccountHolderName).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.OrganizationId, x.SupplierCode }).IsUnique().HasDatabaseName("ux_suppliers_org_code");
            e.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_suppliers_org_status");
            e.HasIndex(x => new { x.OrganizationId, x.RegistrationNumber })
                .IsUnique()
                .HasDatabaseName("ux_suppliers_org_active_registration")
                .HasFilter("status = 'active' AND registration_number IS NOT NULL");
            e.HasOne(x => x.Organization).WithMany(x => x.Suppliers).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<CommitteeDecision>(e =>
        {
            e.ToTable("committee_decisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.DecisionCode).HasMaxLength(20);
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.TotalAmount).HasColumnType("numeric(14,2)");
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.Property(x => x.SuspendReason).HasMaxLength(500);
            e.Property(x => x.ReturnReason).HasMaxLength(500);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.HasIndex(x => new { x.OrganizationId, x.DecisionCode }).IsUnique().HasDatabaseName("ux_committee_decisions_org_code");
            e.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_committee_decisions_org_status");
            e.HasIndex(x => x.FamilyId).HasDatabaseName("ix_committee_decisions_family");
            e.HasOne(x => x.Organization).WithMany(x => x.CommitteeDecisions).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.FamilyId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        });

        modelBuilder.Entity<AssistanceItem>(e =>
        {
            e.ToTable("assistance_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Amount).HasColumnType("numeric(14,2)");
            e.Property(x => x.PaymentTarget).HasMaxLength(20);
            e.Property(x => x.PaymentMethod).HasMaxLength(20);
            e.Property(x => x.PayeeName).HasMaxLength(200);
            e.Property(x => x.VoucherType).HasMaxLength(100);
            e.Property(x => x.ExecutionStatus).HasMaxLength(30);
            e.Property(x => x.ExecutionReference).HasMaxLength(200);
            e.HasIndex(x => new { x.CommitteeDecisionId, x.LineNumber }).IsUnique().HasDatabaseName("ux_assistance_items_decision_line");
            e.HasIndex(x => new { x.OrganizationId, x.ExecutionStatus }).HasDatabaseName("ix_assistance_items_org_execution");
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.CommitteeDecision).WithMany(x => x.Items).HasForeignKey(x => x.CommitteeDecisionId);
            e.HasOne(x => x.AssistanceType).WithMany().HasForeignKey(x => x.AssistanceTypeId);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
            e.HasOne(x => x.Document).WithOne(x => x.AssistanceItem).HasForeignKey<AssistanceItemDocument>(x => x.AssistanceItemId);
            e.HasOne(x => x.PaymentExecution).WithOne(x => x.AssistanceItem).HasForeignKey<PaymentExecution>(x => x.AssistanceItemId);
        });

        modelBuilder.Entity<AssistanceItemDocument>(e =>
        {
            e.ToTable("assistance_item_documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.StoredFileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasIndex(x => x.AssistanceItemId).IsUnique().HasDatabaseName("ux_assistance_item_documents_item");
            e.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId);
        });

        modelBuilder.Entity<PaymentExecution>(e =>
        {
            e.ToTable("payment_executions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.ExecutionReference).HasMaxLength(200);
            e.Property(x => x.ProofFileName).HasMaxLength(255);
            e.Property(x => x.ProofStoredFileName).HasMaxLength(255);
            e.Property(x => x.ReturnReason).HasMaxLength(500);
            e.HasIndex(x => x.AssistanceItemId).IsUnique().HasDatabaseName("ux_payment_executions_item");
            e.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_payment_executions_org_status");
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.CommitteeDecision).WithMany().HasForeignKey(x => x.CommitteeDecisionId);
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

        modelBuilder.Entity<AssistanceTypeSupplier>(e =>
        {
            e.ToTable("assistance_type_suppliers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => new { x.OrganizationId, x.AssistanceTypeId, x.SupplierId })
                .IsUnique()
                .HasDatabaseName("ux_assistance_type_suppliers_org_type_supplier");
            e.HasIndex(x => new { x.OrganizationId, x.AssistanceTypeId })
                .HasDatabaseName("ix_assistance_type_suppliers_org_type");
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.AssistanceType).WithMany(x => x.RelatedSupplierLinks).HasForeignKey(x => x.AssistanceTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
        });

        modelBuilder.Entity<PermissionCatalog>(e =>
        {
            e.ToTable("permission_catalog");
            e.HasKey(x => x.PermissionKey);
            e.Property(x => x.PermissionKey).HasMaxLength(80);
            e.Property(x => x.Category).HasMaxLength(40);
            e.Property(x => x.DisplayNameHe).HasMaxLength(200);
            e.Property(x => x.DescriptionHe).HasMaxLength(500);
            e.HasIndex(x => new { x.Category, x.SortOrder }).HasDatabaseName("ix_permission_catalog_category");
        });

        modelBuilder.Entity<OrganizationRole>(e =>
        {
            e.ToTable("organization_roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.FactoryPresetKey).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasDatabaseName("ux_organization_roles_org_name");
            e.HasIndex(x => x.OrganizationId).HasDatabaseName("ix_organization_roles_org_id");
            e.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_organization_roles_org_status");
            e.HasOne(x => x.Organization).WithMany(x => x.OrganizationRoles).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<OrganizationRoleGrant>(e =>
        {
            e.ToTable("organization_role_grants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.PermissionKey).HasMaxLength(80);
            e.Property(x => x.Scope).HasMaxLength(20);
            e.HasIndex(x => x.OrganizationRoleId).HasDatabaseName("ix_org_role_grants_role_id");
            e.HasIndex(x => x.PermissionKey).HasDatabaseName("ix_org_role_grants_permission_key");
            e.HasIndex(x => new { x.OrganizationRoleId, x.PermissionKey }).IsUnique()
                .HasDatabaseName("ux_org_role_grants_role_key");
            e.HasOne(x => x.OrganizationRole).WithMany(x => x.Grants).HasForeignKey(x => x.OrganizationRoleId);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionKey);
            e.HasOne(x => x.GrantedByUser).WithMany().HasForeignKey(x => x.GrantedByUserId);
        });

        modelBuilder.Entity<UserPermissionOverride>(e =>
        {
            e.ToTable("user_permission_overrides");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.PermissionKey).HasMaxLength(80);
            e.Property(x => x.Effect).HasMaxLength(10);
            e.Property(x => x.Scope).HasMaxLength(20);
            e.HasIndex(x => new { x.UserId, x.PermissionKey }).IsUnique()
                .HasDatabaseName("ux_user_perm_overrides_user_key");
            e.HasIndex(x => x.OrganizationId).HasDatabaseName("ix_user_perm_overrides_org_id");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_user_perm_overrides_effect", "effect IN ('grant', 'deny')");
                t.HasCheckConstraint("ck_user_perm_overrides_scope", "effect = 'deny' OR scope IS NOT NULL");
            });
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.User).WithMany(x => x.PermissionOverrides).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionKey);
            e.HasOne(x => x.GrantedByUser).WithMany().HasForeignKey(x => x.GrantedByUserId);
        });

        modelBuilder.Entity<OrganizationRolePermission>(e =>
        {
            e.ToTable("organization_role_permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Role).HasMaxLength(50);
            e.Property(x => x.PermissionKey).HasMaxLength(80);
            e.HasIndex(x => new { x.OrganizationId, x.Role })
                .HasDatabaseName("ix_org_role_permissions_org_role");
            e.HasIndex(x => new { x.OrganizationId, x.PermissionKey })
                .HasDatabaseName("ix_org_role_permissions_org_key");
            e.HasIndex(x => new { x.OrganizationId, x.Role, x.PermissionKey })
                .IsUnique()
                .HasDatabaseName("ux_org_role_permissions_org_role_key");
            e.HasOne(x => x.Organization).WithMany(x => x.RolePermissions).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionKey);
            e.HasOne(x => x.GrantedByUser).WithMany().HasForeignKey(x => x.GrantedByUserId);
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
