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

        public async Task UpdatePriceOnlyAsync(Guid priceId, decimal price)
        {
            try
            {
                // ✅ Business rule validation (ADD HERE)
                if (price < 0)
                    throw new ArgumentException("Price cannot be negative");

                var entity = await _context.WholeSalerPrices
                    .FirstOrDefaultAsync(x => x.Id == priceId);

                if (entity == null)
                    throw new Exception("Price record not found");

                // Optional: avoid unnecessary DB write
                if (entity.Price == price)
                    return;

                entity.Price = price;
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }
          
        }


        public async Task SavePriceAsync(List<WholeSalerPrice> prices)
        {
            // 1️⃣ Prevent duplicates inside same request (PriceRows duplicates)
            var duplicateInRequest = prices
                .GroupBy(x => new { x.WholesalerId, x.ThreadId, x.UnitId })
                .Any(g => g.Count() > 1);

            if (duplicateInRequest)
                throw new InvalidOperationException(
                    "Duplicate Thread and Unit found in the selected prices."
                );

            // 2️⃣ Prevent duplicates against database
            // <<< PUT YOUR FOREACH DUPLICATE CHECK HERE >>>
            foreach (var price in prices)
            {
                bool exists = await _context.WholeSalerPrices.AnyAsync(x =>
                    x.WholesalerId == price.WholesalerId &&
                    x.ThreadId == price.ThreadId &&
                    x.UnitId == price.UnitId
                );

                if (exists)
                    throw new InvalidOperationException(
                        $"Price already exists Against this Thread and unit'."
                    );
            }

            // 3️⃣ Save safely (only after all checks pass)
            await _context.WholeSalerPrices.AddRangeAsync(prices);
            await _context.SaveChangesAsync();
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
