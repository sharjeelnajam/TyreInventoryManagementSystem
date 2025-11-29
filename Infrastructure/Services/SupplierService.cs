using Domain;
using Domain.DTO;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantProvider _tenantProvider;

        public SupplierService(ApplicationDbContext context, RoleManager<ApplicationRole> roleManager,
            UserManager<ApplicationUser> userManager
            , ITenantProvider tenantProvider)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<Supplier>> GetAllAsync()
        {
            try
            {
                return await _context.Supplier.OrderBy(x => x.Name).ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<Supplier?> GetByIdAsync(Guid id)
        {
            try
            {
                return await _context.Supplier.FirstOrDefaultAsync(x => x.Id == id);
            }
            catch (Exception)
            {

                throw;
            }
         
        }

        public async Task<bool> CreateAsync(Supplier supplier)
        {
            try
            {
                supplier.CreatedAt = DateTime.UtcNow;
                supplier.TenantId = _tenantProvider.TenantId;

                // Save Supplier first
                _context.Supplier.Add(supplier);
                await _context.SaveChangesAsync();

                // -------------------------------
                // Create Identity User
                // -------------------------------

                var supplierUser = new ApplicationUser
                {
                    UserName = supplier.Email,
                    Email = supplier.Email,
                    TenantId = _tenantProvider.TenantId,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(supplierUser, "Supplier@123");

                if (!result.Succeeded)
                    throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

                // -------------------------------
                // Ensure "Supplier" Role Exists
                // -------------------------------

                var existRole = _roleManager.Roles
                    .FirstOrDefault(x => x.Name == "Supplier" && x.TenantId == _tenantProvider.TenantId);

                if (existRole == null)
                {
                    await _roleManager.CreateAsync(new ApplicationRole
                    {
                        Name = "Supplier",
                        TenantId = _tenantProvider.TenantId
                    });
                }
                existRole = _roleManager.Roles
                    .FirstOrDefault(x => x.Name == "Supplier" && x.TenantId == _tenantProvider.TenantId);

                // -------------------------------
                // Assign Role to Supplier User
                // -------------------------------

                _context.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = supplierUser.Id,
                    RoleId = existRole.Id
                });

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Supplier create failed: " + ex.Message);
                return false;
            }
        }


        public async Task<bool> UpdateAsync(Supplier supplier)
        {
            try
            {
                var existing = await _context.Supplier.FindAsync(supplier.Id);
                if (existing == null || existing.IsDeleted) return false;

                _context.Entry(existing).CurrentValues.SetValues(supplier);
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var supplier = await _context.Supplier.FindAsync(id);
                if (supplier == null || supplier.IsDeleted) return false;

               _context.Supplier.Remove(supplier);  
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<TopSupplierDto>> GetTopSuppliersByDateAsync(DateTime start, DateTime end)
        {
            return await _context.Purchase
                .Where(p => p.PurchaseDate >= start && p.PurchaseDate <= end)
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .GroupBy(p => new { p.Supplier.Name })
                .Select(g => new TopSupplierDto
                {
                    SupplierName = g.Key.Name,
                    TotalOrders = g.Count(),
                    TotalQuantity = g.SelectMany(x => x.PurchaseDetails).Sum(x => x.Quantity),
                    TotalAmountPaid = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.TotalAmountPaid)
                .Take(5)
                .ToListAsync();
        }

    }
}
