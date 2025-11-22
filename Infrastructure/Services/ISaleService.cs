using Domain;
using Domain.DTO;
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
        Task<byte[]> GenerateReceiptPdfAsync(Guid saleId);
        Task<List<TopProductDto>> GetTopSellingProductsAsync();
        Task<int> GetTotalInvoicesAsync();
    }
}
