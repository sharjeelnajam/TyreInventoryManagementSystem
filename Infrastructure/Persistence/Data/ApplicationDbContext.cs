using Domain;
using Infrastructure.Persistence.Data;
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

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ITenantProvider _tenantProvider;
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var et in modelBuilder.Model.GetEntityTypes().Where(t => typeof(TenantEntity).IsAssignableFrom(t.ClrType)))
        {
            var param = Expression.Parameter(et.ClrType, "e");
            var prop = Expression.Property(param, nameof(TenantEntity.TenantId));
            var tenantConstant = Expression.Constant(_tenantProvider.TenantId);
            var eq = Expression.Equal(prop, tenantConstant);
            var lambda = Expression.Lambda(eq, param);

            modelBuilder.Entity(et.ClrType).HasQueryFilter(lambda);
        }
    }
    // Example: Add your DbSets here
    // public DbSet<Product> Products { get; set; }
}

