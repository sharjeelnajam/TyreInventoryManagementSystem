using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
   public class ProfitReportService : IProfitReportService
    {
        private readonly ApplicationDbContext _context;
        public ProfitReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetProfitAsync(DateTime start, DateTime end)
        {
            try
            {
                return await _context.ProfitHistories
               .Where(p => p.RecordedAt >= start && p.RecordedAt < end)
               .SumAsync(p => p.ProfitAmount);
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public Task<decimal> GetTodayProfitAsync()
        {
            try
            {
                var start = DateTime.Today;
                var end = start.AddDays(1);
                return GetProfitAsync(start, end);
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public Task<decimal> GetWeekProfitAsync()
        {
            try
            {
                var start = DateTime.Today.AddDays(-7);
                var end = DateTime.Today.AddDays(1);
                return GetProfitAsync(start, end);
            }
            catch (Exception)
            {

                throw;
            }
         
        }

        public Task<decimal> GetMonthProfitAsync()
        {
            try
            {
                var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var end = start.AddMonths(1);
                return GetProfitAsync(start, end);
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        public Task<decimal> GetYearProfitAsync()
        {
            try
            {
                var start = new DateTime(DateTime.Today.Year, 1, 1);
                var end = start.AddYears(1);
                return GetProfitAsync(start, end);
            }
            catch (Exception)
            {

                throw;
            }
          
        }

        public async Task<decimal> GetTotalProfitAsync()
        {
            try
            {
                return await _context.ProfitHistories.SumAsync(p => p.ProfitAmount);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<(string ProductName, decimal Profit)>> GetProfitByProduct(DateTime start, DateTime end)
        {
            try
            {
                var query = await _context.ProfitHistories
              .Where(p => p.RecordedAt >= start && p.RecordedAt < end)
              .GroupBy(p => p.ProductId)
              .Select(g => new
              {
                  ProductId = g.Key,
                  Profit = g.Sum(x => x.ProfitAmount)
              })
              .ToListAsync();

                var result = new List<(string ProductName, decimal Profit)>();

                foreach (var item in query)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    result.Add((product?.ProductName ?? "Unknown", item.Profit));
                }

                return result;
            }
            catch (Exception)
            {

                throw;
            }
          
        }
    }
}
