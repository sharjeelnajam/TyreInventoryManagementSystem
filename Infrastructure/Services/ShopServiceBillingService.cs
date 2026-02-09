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

        public ShopServiceBillingService(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
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
            bill.WholesalerId = null;
            bill.WholesalerName = null;
            await _context.SaveChangesAsync();
        }

        public async Task UpdateBillWholesalerAsync(Guid billId, Guid? wholesalerId, string? wholesalerName)
        {
            var bill = await _context.ShopServiceBills.FindAsync(billId);
            if (bill == null || bill.Status != ShopServiceBillStatus.Open)
                return;
            bill.WholesalerId = wholesalerId;
            bill.WholesalerName = wholesalerName;
            bill.CustomerId = null;
            bill.CustomerName = null;
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

        public async Task<ShopServiceBillItem> AddItemAsync(Guid billId, Guid? serviceId, string serviceName, int quantity, decimal unitPrice, string? remarks = null)
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

        public async Task CheckoutAsync(Guid billId, string paymentMethod, string paymentStatus, string? notes = null)
        {
            var bill = await _context.ShopServiceBills
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == billId);
            if (bill == null || bill.Status != ShopServiceBillStatus.Open)
                throw new InvalidOperationException("Bill not found or already closed.");

            bill.TotalAmount = Math.Max(0, bill.Items.Sum(i => i.TotalPrice) - bill.Discount);
            bill.ClosedAt = DateTime.UtcNow;
            bill.Status = ShopServiceBillStatus.Closed;
            bill.PaymentMethod = paymentMethod;
            bill.PaymentStatus = paymentStatus;
            bill.Notes = notes;
            await _context.SaveChangesAsync();
        }
    }
}
