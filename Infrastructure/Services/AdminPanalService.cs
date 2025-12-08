using Domain;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class AdminPanalService : IAdminPanalService
    {
        private readonly ApplicationDbContext _context;
        public AdminPanalService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tenant>> GetAllTenants()
        {
            return await _context.Tenants
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<Customer>> GetCustomers(Guid? tenantId)
        {
            try
            {
                return await _context.Customer
               .Where(c => !tenantId.HasValue || c.TenantId == tenantId)
               .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task<List<Product>> GetProducts(Guid? tenantId)
        {
            try
            {
                return await _context.Products
               .Where(c => !tenantId.HasValue || c.TenantId == tenantId)
               .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
           
        }  
        
        public async Task<List<Supplier>> GetSuppliers(Guid? tenantId)
        {
            try
            {
                return await _context.Supplier
               .Where(c => !tenantId.HasValue || c.TenantId == tenantId)
               .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task<List<Staff>> GetStaff(Guid? tenantId)
        {
            try
            {
                return await _context.Staff
              .Where(s => !tenantId.HasValue || s.TenantId == tenantId)
              .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
          
        }

        public async Task<decimal> GetTotalExpense(Guid? tenantId)
        {
            try
            {
                return await _context.Expenses
                .Where(e => !tenantId.HasValue || e.TenantId == tenantId)
                .SumAsync(e => (decimal?)e.Amount ?? 0);
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        public async Task<int> GetTotalInvoices(Guid? tenantId)
        {
            try
            {
                return await _context.Sale
                .Where(inv => !tenantId.HasValue || inv.TenantId == tenantId)
                .CountAsync();
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        public async Task<List<Wholesaler>> GetWholeSalers(Guid? tenantId)
        {
            try
            {
                return await _context.Wholesalers
               .Where(c => !tenantId.HasValue || c.TenantId == tenantId)
               .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<decimal> GetTotalPurchaseAmountAsync(Guid? tenantId)
        {
            try
            {
                return await _context.Purchase.Where(c => !tenantId.HasValue || c.TenantId == tenantId).SumAsync(p => p.NetAmount);
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task<decimal> GetTotalSalesAsync(Guid? tenantId)
        {
            try
            {
                return await _context.Sale
              .Where(s => !tenantId.HasValue || s.TenantId == tenantId)
              .SumAsync(s => s.NetAmount);
            }
            catch (Exception)
            {

                throw;
            }
          
        }  
        
        public async Task<decimal> GetTotalProfit(Guid? tenantId)
        {
            try
            {
                return await _context.ProfitHistories
                 .Where(s => !tenantId.HasValue || s.TenantId == tenantId)
                         .SumAsync(p => p.ProfitAmount);
            }
            catch (Exception)
            {

                throw;
            }
          
        }
    }
}
