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
        Task<List<TodayPurchaseReportDto>> GetPurchasesByDateRange(DateTime from, DateTime to);
        Task<List<TodaySaleReportDto>> GetSalesByDateRange(DateTime from, DateTime to);
        Task<SalePaymentSummaryDto> GetSalePaymentSummaryByDateRange(DateTime from, DateTime to);

        Task<List<AvailableStockDto>> GetAvailableStock();
    }
}
