using Domain;
using Domain.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

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


        //// ✅ ApplicationUser configuration
        //modelBuilder.Entity<ApplicationUser>(entity =>
        //{
        //    // Index TenantId (fast lookups ke liye)
        //    entity.HasIndex(u => u.TenantId);

        //    // Relationship: Tenant → Users
        //    entity.HasOne(u => u.Tenant)
        //          .WithMany(t => t.Users)
        //          .HasForeignKey(u => u.TenantId)
        //          .OnDelete(DeleteBehavior.Restrict);
        //});


         // Apply Tenant Query Filters for all entities inheriting MultiTenantEntity
        foreach (var et in modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(MultiTenantEntity).IsAssignableFrom(t.ClrType)))
            {
                var param = Expression.Parameter(et.ClrType, "e");
                var prop = Expression.Property(param, nameof(MultiTenantEntity.TenantId));
                var tenantConstant = Expression.Constant(_tenantProvider.TenantId);
                var eq = Expression.Equal(prop, tenantConstant);
                var lambda = Expression.Lambda(eq, param);

                modelBuilder.Entity(et.ClrType).HasQueryFilter(lambda);
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

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    // ✅ Ensure interceptor is added to the EF pipeline
    //    optionsBuilder.AddInterceptors(_tenantInterceptor);
    //    base.OnConfiguring(optionsBuilder);
    //}
    // Example: Add your DbSets here

    public DbSet<Tenant> Tenants { get; set; }
    // public DbSet<Product> Products { get; set; }
}

