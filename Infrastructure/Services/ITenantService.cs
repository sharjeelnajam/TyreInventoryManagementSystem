using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Infrastructure.Services
{
    public interface ITenantService
    {
        Task<Tenant> GetTenantByIdAsync(Guid id);
        Task<List<Tenant>> GetTenantsAsync();
        Task<Tenant?> GetTenantByDomainAsync(string domain);
        Task<Tenant> AddTenantAsync(Tenant tenant, Guid ownerUserId);
        Task<Tenant?> UpdateTenantAsync(Tenant tenant, Guid? ownerUserId);
        Task<bool> DeleteTenantAsync(Guid id);

    }
}
