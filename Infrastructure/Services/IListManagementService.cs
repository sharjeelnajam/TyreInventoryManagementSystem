using Domain;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IListManagementService
    {
        Task<List<ListManagement>> GetAllAsync();
        Task<List<ListManagement>> GetByTypeAsync(ListType type);
        Task AddAsync(ListManagement entity);
        Task UpdateAsync(ListManagement entity);
        Task SoftDeleteAsync(Guid id);
    }
}
