using Domain;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ListManagementService : IListManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ListManagementService(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<ListManagement>> GetByTypeAsync(ListType type)
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    var list = await _context.ListManagements
                    .Where(x => x.Type == type && x.TenantId == _tenantProvider.TenantId)
                     .OrderBy(x => x.Name)
                     .ToListAsync();
                    return list;
                }
                return new List<ListManagement>();
            }
            catch (Exception)
            {

                throw;
            }
         
        }

        public async Task AddAsync(ListManagement entity)
        {
            entity.Id = Guid.NewGuid();
            _context.ListManagements.Add(entity);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateAsync(ListManagement entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _context.ListManagements.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            var item = await _context.ListManagements.FindAsync(id);
            if (item == null) return;

            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}
