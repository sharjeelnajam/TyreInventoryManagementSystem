using Domain.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IReportService
    {
        Task<List<TodayPurchaseReportDto>> GetTodayPurchases();
        Task<List<TodaySaleReportDto>> GetTodaySales();
        Task<List<AvailableStockDto>> GetAvailableStock();
    }
}
