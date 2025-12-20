using Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class WholesalerPriceService : IWholesalerPriceService
    {
        private readonly ApplicationDbContext _context;
        public WholesalerPriceService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task SavePricesAsync(Guid wholesalerId, List<WholeSalerPrice> prices)
        {
            try
            {
                var existing = _context.WholeSalerPrices.Where(x => x.WholesalerId == wholesalerId);

                _context.WholeSalerPrices.RemoveRange(existing);
                await _context.WholeSalerPrices.AddRangeAsync(prices);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<List<WholeSalerPrice>> GetByWholesalerIdAsync(Guid wholesalerId)
        {
            try
            {
                return await _context.WholeSalerPrices
                    .Where(x => x.WholesalerId == wholesalerId)
                    .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
            
        }

    }
}
