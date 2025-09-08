using Application.Contracts;
using Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence
{
   public class AppDbContext : DbContext
    {
        private readonly ITenantProvider _tenantProvider;
        public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options) => _tenantProvider = tenantProvider;

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
    }
}
