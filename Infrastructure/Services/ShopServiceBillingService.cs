using Domain;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;

namespace Infrastructure.Services
{
    public class ShopServiceBillingService : IShopServiceBillingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly ISaleService _saleService;

        public ShopServiceBillingService(ApplicationDbContext context, ITenantProvider tenantProvider, ISaleService saleService)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _saleService = saleService;
        }

        public async Task<ShopServiceBill> StartBillAsync(Guid? customerId = null, string? customerName = null)
        {
            var bill = new ShopServiceBill
            {
                Id = Guid.NewGuid(),
                BillNumber = $"SB-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                Status = ShopServiceBillStatus.Open,
                CustomerId = customerId,
                CustomerName = customerName,
                OpenedAt = DateTime.UtcNow,
                TotalAmount = 0
            };
            _context.ShopServiceBills.Add(bill);
            await _context.SaveChangesAsync();
            return bill;
        }

        public async Task<List<ShopServiceBill>> GetOpenBillsAsync()
        {
            return await _context.ShopServiceBills
                .Where(b => b.Status == ShopServiceBillStatus.Open)
                .OrderByDescending(b => b.OpenedAt)
                .ToListAsync();
        }

        public async Task<List<ShopServiceBill>> GetClosedBillsAsync(DateTime? from = null, DateTime? to = null)
        {
            var query = _context.ShopServiceBills
                .Where(b => b.Status == ShopServiceBillStatus.Closed);
            if (from.HasValue)
                query = query.Where(b => b.ClosedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(b => b.ClosedAt <= to.Value);
            return await query.OrderByDescending(b => b.ClosedAt).ToListAsync();
        }

        public async Task<ShopServiceBill?> GetBillByIdAsync(Guid billId)
        {
            return await _context.ShopServiceBills
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == billId);
        }

        public async Task UpdateBillCustomerAsync(Guid billId, Guid? customerId, string? customerName)
        {
            var bill = await _context.ShopServiceBills.FindAsync(billId);
            if (bill == null || bill.Status != ShopServiceBillStatus.Open)
                return;
            bill.CustomerId = customerId;
            bill.CustomerName = customerName;
            await _context.SaveChangesAsync();
        }

        public async Task UpdateBillDiscountAsync(Guid billId, decimal discount)
        {
            var bill = await _context.ShopServiceBills.FindAsync(billId);
            if (bill == null || bill.Status != ShopServiceBillStatus.Open)
                return;
            bill.Discount = discount;
            await _context.SaveChangesAsync();
        }

        public async Task<ShopServiceBillItem> AddItemAsync(Guid billId, Guid? serviceId, Guid? productId, string serviceName, int quantity, decimal unitPrice, string? remarks = null)
        {
            var bill = await _context.ShopServiceBills.FindAsync(billId);
            if (bill == null || bill.Status != ShopServiceBillStatus.Open)
                throw new InvalidOperationException("Bill not found or already closed.");

            var totalPrice = quantity * unitPrice;
            var item = new ShopServiceBillItem
            {
                Id = Guid.NewGuid(),
                ShopServiceBillId = billId,
                ServiceId = serviceId,
                ProductId = productId,
                ServiceName = serviceName,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice,
                Remarks = remarks
            };
            _context.ShopServiceBillItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task UpdateItemAsync(Guid itemId, int quantity, decimal unitPrice)
        {
            var item = await _context.ShopServiceBillItems
                .Include(i => i.ShopServiceBill)
                .FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null || item.ShopServiceBill.Status != ShopServiceBillStatus.Open)
                throw new InvalidOperationException("Item not found or bill is closed.");

            item.Quantity = quantity;
            item.UnitPrice = unitPrice;
            item.TotalPrice = quantity * unitPrice;
            await _context.SaveChangesAsync();
        }

        public async Task RemoveItemAsync(Guid itemId)
        {
            var item = await _context.ShopServiceBillItems
                .Include(i => i.ShopServiceBill)
                .FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null || item.ShopServiceBill.Status != ShopServiceBillStatus.Open)
                throw new InvalidOperationException("Item not found or bill is closed.");

            _context.ShopServiceBillItems.Remove(item);
            await _context.SaveChangesAsync();
        }

        public async Task SaveBillAsync(Guid billId)
        {
            var bill = await _context.ShopServiceBills.FindAsync(billId);
            if (bill == null || bill.Status != ShopServiceBillStatus.Open)
                throw new InvalidOperationException("Bill not found or already closed.");
            bill.PaymentStatus = "Hold";
            await _context.SaveChangesAsync();
        }

        public async Task<Guid> CheckoutAsync(Guid billId, string paymentMethod, string paymentStatus, string? notes = null, decimal? cashAmount = null, decimal? cardAmount = null)
        {
            var bill = await _context.ShopServiceBills
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == billId);
            if (bill == null || bill.Status != ShopServiceBillStatus.Open)
                throw new InvalidOperationException("Bill not found or already closed.");

            var subtotal = bill.Items.Sum(i => i.TotalPrice);
            var netAmount = Math.Max(0, subtotal - bill.Discount);

            // Validate split payment: Card + Cash must equal total when using "Card & Cash"
            if (string.Equals(paymentMethod, "Card & Cash", StringComparison.OrdinalIgnoreCase))
            {
                var sum = (cashAmount ?? 0) + (cardAmount ?? 0);
                if (Math.Abs(sum - netAmount) > 0.01m)
                    throw new InvalidOperationException($"Split payment invalid: Card (£{(cardAmount ?? 0):N2}) + Cash (£{(cashAmount ?? 0):N2}) = £{sum:N2} must equal Total £{netAmount:N2}. Checkout not allowed.");
            }

            bill.TotalAmount = netAmount;
            bill.ClosedAt = DateTime.UtcNow;
            bill.Status = ShopServiceBillStatus.Closed;
            bill.PaymentMethod = paymentMethod;
            bill.PaymentStatus = paymentStatus;
            bill.Notes = notes;
            await _context.SaveChangesAsync();

            // Build SaleDetails from bill items that are products (for stock update)
            var saleDetails = new List<SaleDetail>();
            foreach (var item in bill.Items)
            {
                if (item.ProductId.HasValue && item.ProductId != Guid.Empty)
                {
                    var product = await _context.Products.FindAsync(item.ProductId.Value);
                    var costPrice = product?.AverageCostPrice ?? 0;
                    saleDetails.Add(new SaleDetail
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId.Value,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice,
                        CostPrice = costPrice,
                        ProfitAmount = Math.Round((item.UnitPrice - costPrice) * item.Quantity, 2)
                    });
                }
            }

            var sale = new Sale
            {
                SaleNumber = bill.BillNumber,
                SaleDate = bill.ClosedAt.Value,
                CustomerId = bill.CustomerId,
                CustomerName = bill.CustomerName,
                TotalAmount = subtotal,
                Discount = bill.Discount,
                TaxAmount = 0,
                NetAmount = netAmount,
                PaymentMethod = paymentMethod ?? "Cash",
                PaymentStatus = paymentStatus ?? "Paid",
                CashAmount = cashAmount ?? (paymentMethod == "Cash" ? netAmount : 0),
                CardAmount = cardAmount ?? (paymentMethod == "Card" ? netAmount : 0),
                IsApproved = true,
                Notes = notes,
                ShopServiceBillId = bill.Id,
                SaleDetails = saleDetails
            };
            await _saleService.AddAsync(sale);
            return sale.Id;
        }
    }
}
