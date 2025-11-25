using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IWholesalerService
    {
        Task<List<Wholesaler>> GetAllAsync();
        Task<Wholesaler> GetByIdAsync(Guid id);
        Task<bool> AddAsync(Wholesaler wholesaler);
        Task<bool> UpdateAsync(Wholesaler wholesaler);
        Task<bool> DeleteAsync(Guid id);
    }
}
