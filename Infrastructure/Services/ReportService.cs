using Domain;
using Domain.DTO;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        public ReportService(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        /// <summary>Purchases for the whole day(s): from 00:00:00 of fromDate through end of toDate.</summary>
        public async Task<List<TodayPurchaseReportDto>> GetPurchasesByDateRange(
     DateTime fromDate, DateTime toDate)
        {
            try
            {
                var start = fromDate.Date;
                var end = toDate.Date.AddDays(1); // exclusive: whole day(s)

                return await _context.PurchaseDetails
                    .Where(pd =>
                        !pd.IsDeleted &&
                        pd.Purchase.PurchaseDate >= start &&
                        pd.Purchase.PurchaseDate < end &&
                        (TenantId == null || pd.Purchase.TenantId == TenantId)
                    )
                    .Select(pd => new TodayPurchaseReportDto
                    {
                        SupplierName = pd.Purchase.Supplier.Name,
                        ProductName = pd.Product.ProductName,
                        Quantity = pd.Quantity,
                        UnitPrice = pd.UnitPrice,
                        TotalPrice = pd.TotalPrice,
                        PurchaseDate = pd.Purchase.PurchaseDate
                    })
                    .ToListAsync();
            }
            catch
            {
                throw;
            }
        }

        /// <summary>Sales for the whole day(s): from 00:00:00 of fromDate through end of toDate.</summary>
        public async Task<List<TodaySaleReportDto>> GetSalesByDateRange(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var start = fromDate.Date;
                var end = toDate.Date.AddDays(1); // exclusive: whole day(s)

                var sales = await (
                    from sd in _context.SaleDetail
                    join s in _context.Sale on sd.SaleId equals s.Id
                    join c in _context.Customer on s.CustomerId equals c.Id into cust
                    from c in cust.DefaultIfEmpty()   // LEFT JOIN
                    where !sd.IsDeleted
                       && s.SaleDate >= start
                       && s.SaleDate < end
                       && !s.IsReturn
                       && (TenantId == null || s.TenantId == TenantId)
                    select new TodaySaleReportDto
                    {
                        CustomerName = c != null ? c.Name : "Walk-in Customer",
                        ProductName = sd.Product.ProductName,
                        Quantity = sd.Quantity,
                        UnitPrice = sd.UnitPrice,
                        TotalPrice = sd.TotalPrice,
                        SaleDate = s.SaleDate
                    }
                ).ToListAsync();

                return sales;
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<AvailableStockDto>> GetAvailableStock()
        {
            try
            {
                return await _context.StockHistories
                  .Where(x => !x.IsDeleted && (TenantId == null || x.TenantId == TenantId))
                  .GroupBy(x => new { x.ProductId, x.Product.ProductName })
                  .Select(g => new AvailableStockDto
                  {
                      ProductName = g.Key.ProductName,
                      CurrentStock = g
                             .OrderByDescending(x => x.ActionDate)
                             .First().NewStockLevel
                  })
                  .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
        }

        private Guid? TenantId => _tenantProvider.TenantId != Guid.Empty ? _tenantProvider.TenantId : null;
    }


}
