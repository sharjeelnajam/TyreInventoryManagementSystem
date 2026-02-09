using Domain;
using Domain.Enums;

namespace Infrastructure.Services
{
    public interface IShopServiceBillingService
    {
        Task<ShopServiceBill> StartBillAsync(Guid? customerId = null, string? customerName = null);
        Task<List<ShopServiceBill>> GetOpenBillsAsync();
        Task<List<ShopServiceBill>> GetClosedBillsAsync(DateTime? from = null, DateTime? to = null);
        Task<ShopServiceBill?> GetBillByIdAsync(Guid billId);
        Task UpdateBillCustomerAsync(Guid billId, Guid? customerId, string? customerName);
        Task UpdateBillWholesalerAsync(Guid billId, Guid? wholesalerId, string? wholesalerName);
        Task UpdateBillDiscountAsync(Guid billId, decimal discount);
        Task<ShopServiceBillItem> AddItemAsync(Guid billId, Guid? serviceId, string serviceName, int quantity, decimal unitPrice, string? remarks = null);
        Task UpdateItemAsync(Guid itemId, int quantity, decimal unitPrice);
        Task RemoveItemAsync(Guid itemId);
        Task SaveBillAsync(Guid billId);
        Task CheckoutAsync(Guid billId, string paymentMethod, string paymentStatus, string? notes = null);
    }
}
