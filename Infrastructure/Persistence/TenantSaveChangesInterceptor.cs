using Application.Contracts;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence
{
    public class TenantSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly ITenantProvider _tenantProvider;

        public TenantSaveChangesInterceptor(ITenantProvider tenantProvider) => _tenantProvider = tenantProvider;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var ctx = eventData.Context;
            if (ctx != null)
            {
                foreach (var entry in ctx.ChangeTracker.Entries<TenantEntity>())
                {
                    if (entry.State == EntityState.Added)
                        entry.Entity.TenantId = _tenantProvider.TenantId;
                }
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
