using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface ISaleService
    {
        Task<List<Sale>> GetAllAsync();
        Task<Sale?> GetByIdAsync(Guid id);
        Task<bool> AddAsync(Sale sale);
        Task UpdateAsync(Sale sale);
        Task<bool> DeleteAsync(Guid id);
        Task<List<SaleDetail>> GetSaleDetailsByProductIdAsync(Guid productId);
    }
}
