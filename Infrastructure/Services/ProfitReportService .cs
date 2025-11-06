using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        // New method for date range profit
        public async Task<decimal> GetProfitByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Include the end date in the range by adding one day and using less than
                var adjustedEndDate = endDate.AddDays(1);
                return await _context.ProfitHistories
                    .Where(p => p.RecordedAt >= startDate && p.RecordedAt < adjustedEndDate)
                    .SumAsync(p => p.ProfitAmount);
            }
            catch (Exception)
            {
                throw;
            }
        }

        // New method for yearly profit data as dictionary
        //public async Task<Dictionary<string, decimal>> GetYearlyProfitDataAsync(int year)

        //{
        //    try
        //    {
        //        var yearlyProfit = new Dictionary<string, decimal>();

        //        for (int month = 1; month <= 12; month++)
        //        {
        //            var monthStart = new DateTime(year, month, 1);
        //            var monthEnd = monthStart.AddMonths(1);

        //            var monthlyProfit = await GetProfitAsync(monthStart, monthEnd);

        //            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month);
        //            yearlyProfit[monthName] = monthlyProfit;
        //        }

        //        return yearlyProfit;
        //    }
        //    catch (Exception)
        //    {
        //        throw;
        //    }
        //}

        public async Task<Dictionary<string, decimal>> GetYearlyProfitDataAsync(int year)
        {
            // Replace this with DB logic to group by months for the given year
            return new Dictionary<string, decimal>
        {
            { "Jan", 2000 },
            { "Feb", 1500 },
            { "Mar", 3200 },
            { "Apr", 2500 },
            { "May", 4000 },
            { "Jun", 2800 },
            { "Jul", 3000 },
            { "Aug", 2700 },
            { "Sep", 3600 },
            { "Oct", 4200 },
            { "Nov", 3100 },
            { "Dec", 5000 }
        };
        }
    }
}
