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
        Task<List<Sale>> GetAllSalesAsync();
        Task<bool> ExistsAsync(Guid id);
        Task<Sale?> GetSaleByIdAsync(Guid id);
        Task AddSaleAsync(Sale sale);
        Task UpdateSaleAsync(Sale sale);
        Task<List<Customer>> GetCustomersAsync();
        Task<List<Product>> GetProductsAsync();
        Task DeleteSaleAsync(Guid id);
    }
}
