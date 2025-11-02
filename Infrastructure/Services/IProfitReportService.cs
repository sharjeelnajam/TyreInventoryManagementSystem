using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
   public interface IProfitReportService
    {
        Task<decimal> GetProfitAsync(DateTime start, DateTime end);
        Task<decimal> GetTodayProfitAsync();
        Task<decimal> GetWeekProfitAsync();
        Task<decimal> GetMonthProfitAsync();
        Task<decimal> GetYearProfitAsync();
        Task<decimal> GetTotalProfitAsync();
        Task<List<(string ProductName, decimal Profit)>> GetProfitByProduct(DateTime start, DateTime end);
    }
}
