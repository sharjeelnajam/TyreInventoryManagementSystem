using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IPurchaseService
    {
        Task<List<Purchase>> GetAllPurchasesAsync();
        Task<Purchase> GetPurchaseByIdAsync(Guid id);
        Task AddPurchaseAsync(Purchase purchase);
        Task UpdatePurchaseAsync(Purchase purchase);
        Task DeletePurchaseAsync(Guid id);
        Task<List<PurchaseDetail>> GetPurchaseDetailsByProductIdAsync(Guid productId);
    }
}
