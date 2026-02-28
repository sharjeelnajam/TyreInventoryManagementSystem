using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IAdminPanalService
    {
        Task<List<Tenant>> GetAllTenants();
        Task<List<Customer>> GetCustomers(Guid? tenantId);
        Task<List<Product>> GetProducts(Guid? tenantId);
        Task<List<Staff>> GetStaff(Guid? tenantId);
        Task<decimal> GetTotalExpense(Guid? tenantId);
        Task<int> GetTotalInvoices(Guid? tenantId);
        Task<List<Supplier>> GetSuppliers(Guid? tenantId);
        Task<List<Customer>> GetWholesalerCustomers(Guid? tenantId);
        Task<decimal> GetTotalPurchaseAmountAsync(Guid? tenantId);
        Task<decimal> GetTotalSalesAsync(Guid? tenantId);
        Task<decimal> GetTotalProfit(Guid? tenantId);

    }
}
