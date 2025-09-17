using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.MultiTenancy;

namespace Infrastructure.Persistence
{
    public class MultiTenantSaveChangesInterceptor(ITenantProvider tenantProvider) : SaveChangesInterceptor
    {
        private readonly ITenantProvider _tenantProvider = tenantProvider;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ApplyTenantAndAudit(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ApplyTenantAndAudit(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void ApplyTenantAndAudit(DbContext? context)
        {
            if (context == null) return;

            var now = DateTime.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
            {
                // TenantId assign if entity is MultiTenantEntity
                if (entry.Entity is MultiTenantEntity multiTenantEntity)
                {
                    if (entry.State == EntityState.Added && multiTenantEntity.TenantId == Guid.Empty)
                    {
                        multiTenantEntity.TenantId = _tenantProvider.TenantId;
                    }
                }

                // Audit fields
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        break;

                    case EntityState.Deleted:
                        entry.State = EntityState.Modified; // soft delete
                        entry.Entity.DeletedAt = now;
                        break;
                }
            }
        }
    }
}
