using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
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
        private readonly ITenantProvider _tenantProvider;
        public ProfitReportService(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<decimal> GetProfitAsync(DateTime start, DateTime end)
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    // Filter by Sale.SaleDate (when sale occurred) for sales, else RecordedAt
                    var fromSales = await _context.ProfitHistories
                        .Where(p => p.SaleId != null && p.TenantId == _tenantProvider.TenantId)
                        .Join(_context.Sale, ph => ph.SaleId, s => s.Id, (ph, s) => new { ph, s })
                        .Where(x => x.s.SaleDate >= start && x.s.SaleDate < end)
                        .SumAsync(x => x.ph.ProfitAmount);

                    var fromOthers = await _context.ProfitHistories
                        .Where(p => p.SaleId == null && p.RecordedAt >= start && p.RecordedAt < end && p.TenantId == _tenantProvider.TenantId)
                        .SumAsync(p => p.ProfitAmount);

                    return fromSales + fromOthers;
                }
                else return 0m;
               
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
                if(_tenantProvider.TenantId != Guid.Empty)
                {
                    var query = await _context.ProfitHistories
                   .Where(p => p.RecordedAt >= start && p.RecordedAt < end && p.TenantId == _tenantProvider.TenantId)
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
                else return new List<(string ProductName, decimal Profit)>();

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
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    // Include the end date in the range by adding one day and using less than
                    var start = startDate.Date;
                    var adjustedEndDate = endDate.Date.AddDays(1);

                    // Compute profit dynamically from SaleDetail (UnitPrice - CostPrice) * Quantity
                    // so it works even if ProfitHistories is incomplete on live.
                    var profitFromDetails = await (
                        from sd in _context.SaleDetail
                        join s in _context.Sale on sd.SaleId equals s.Id
                        where s.SaleDate >= start
                              && s.SaleDate < adjustedEndDate
                              && !s.IsReturn
                              && s.TenantId == _tenantProvider.TenantId
                        select sd.ProfitAmount
                    ).SumAsync();

                    return profitFromDetails;
                }
                else return 0m;
               
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
