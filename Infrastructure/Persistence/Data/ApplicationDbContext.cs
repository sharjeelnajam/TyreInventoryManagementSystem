using Domain;
using Domain.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System.Linq.Expressions;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly MultiTenantSaveChangesInterceptor _tenantInterceptor;
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenantProvider,
        MultiTenantSaveChangesInterceptor tenantInterceptor)
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _tenantInterceptor = tenantInterceptor;

    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // Apply IsDeleted == false filter to all entities inheriting BaseEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");

                // e => !e.IsDeleted
                var isDeletedFilter = Expression.Equal(
                    Expression.Property(parameter, nameof(BaseEntity.IsDeleted)),
                    Expression.Constant(false)
                );

                // e => e.TenantId == _tenantId
                //var tenantFilter = Expression.Equal(
                //    Expression.Property(parameter, nameof(BaseEntity.TenantId)),
                //    Expression.Constant(_tenantProvider.TenantId)
                //);

                // combine both: e => !e.IsDeleted && e.TenantId == _tenantId
                //var combined = Expression.AndAlso(isDeletedFilter, tenantFilter);

                var lambda = Expression.Lambda(isDeletedFilter, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        // ✅ SUPER ADMIN Seeding (sirf ek martaba, loop ke baahar)
        var superAdminRoleId = Guid.NewGuid();
        var superAdminUserId = Guid.NewGuid();

        // Role
        modelBuilder.Entity<ApplicationRole>().HasData(new ApplicationRole
        {
            Id = superAdminRoleId,
            Name = "SuperAdmin",
            NormalizedName = "SUPERADMIN"
        });

        // User
        var hasher = new PasswordHasher<ApplicationUser>();
        var superAdmin = new ApplicationUser
        {
            Id = superAdminUserId,
            UserName = "superadmin@system.com",
            NormalizedUserName = "SUPERADMIN@SYSTEM.COM",
            Email = "superadmin@system.com",
            NormalizedEmail = "SUPERADMIN@SYSTEM.COM",
            EmailConfirmed = true,
            TenantId = null, // ✅ SuperAdmin ke liye null
            SecurityStamp = Guid.NewGuid().ToString("D"),
            PasswordHash = hasher.HashPassword(null, "Admin@123")
        };

        modelBuilder.Entity<ApplicationUser>().HasData(superAdmin);

        // User-Role Mapping
        modelBuilder.Entity<IdentityUserRole<Guid>>().HasData(new IdentityUserRole<Guid>
        {
            RoleId = superAdminRoleId,
            UserId = superAdminUserId
        });
    }
    // Example: Add your DbSets here

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Staff> Staff { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Supplier> Supplier { get; set; }
    public DbSet<Customer> Customer { get; set; }
    public DbSet<Purchase> Purchase { get; set; }
    public DbSet<Sale> Sale { get; set; }
    public DbSet<SaleDetail> SaleDetail { get; set; }
    public DbSet<PurchaseDetail> PurchaseDetails { get; set; }
    public DbSet<StockHistory> StockHistories { get; set; }
}

