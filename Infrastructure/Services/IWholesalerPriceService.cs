using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IWholesalerPriceService
    {
        Task SavePricesAsync(Guid customerId, List<CustomerSalerPrice> prices);
        Task SavePriceAsync(List<CustomerSalerPrice> prices);
        Task<List<CustomerSalerPrice>> GetByCustomerIdAsync(Guid customerId);
        Task UpdatePriceOnlyAsync(Guid priceId, decimal price);
    }
}
