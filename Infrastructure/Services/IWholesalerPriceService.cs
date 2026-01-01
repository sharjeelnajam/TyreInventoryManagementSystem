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
        Task SavePricesAsync(Guid wholesalerId, List<WholeSalerPrice> prices);
        Task SavePriceAsync(List<WholeSalerPrice> prices);
        Task<List<WholeSalerPrice>> GetByWholesalerIdAsync(Guid wholesalerId);
        Task UpdatePriceOnlyAsync(Guid priceId, decimal price);


    }
}
