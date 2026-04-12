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
        Task UpdateBillDiscountAsync(Guid billId, decimal discount);
        Task<ShopServiceBillItem> AddItemAsync(Guid billId, Guid? serviceId, Guid? productId, string serviceName, int quantity, decimal unitPrice, string? remarks = null);
        Task UpdateItemAsync(Guid itemId, int quantity, decimal unitPrice);
        Task RemoveItemAsync(Guid itemId);
        Task SaveBillAsync(Guid billId);
        Task<Guid> CheckoutAsync(Guid billId, string paymentMethod, string paymentStatus, string? notes = null, string? jobDescription = null, decimal? cashAmount = null, decimal? cardAmount = null);
    }
}
