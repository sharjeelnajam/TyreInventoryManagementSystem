using Domain;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public TenantService(ApplicationDbContext context, ITenantProvider tenantProvider, RoleManager<ApplicationRole> applicationRole, UserManager<ApplicationUser> applicationUser)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _userManager = applicationUser;
            _roleManager = applicationRole;
        }

        public async Task<Tenant> GetTenantByIdAsync(Guid id)
        {
            try
            {
                var tenant = await _context.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (tenant == null)
                    throw new KeyNotFoundException($"Tenant with Id '{id}' not found.");

                return tenant;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while retrieving the tenant by ID.", ex);
            }
        }

        public async Task<List<Tenant>> GetTenantsAsync()
        {
            try
            {
                return await _context.Tenants
                    .Where(t => !t.IsDeleted)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while retrieving tenants.", ex);
            }
        }

        public async Task<Tenant?> GetTenantByDomainAsync(string domain)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(domain))
                    return null;

                return await _context.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Domain.ToLower() == domain.ToLower());
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while retrieving tenant by domain.", ex);
            }
        }

        public async Task<Tenant> AddTenantAsync(Tenant ten, Guid ownerUserId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ten.Name))
                    throw new ArgumentException("name is required.", nameof(ten.Name));

                if (string.IsNullOrWhiteSpace(ten.Domain))
                    throw new ArgumentException("domain is required.", nameof(ten.Domain));

                var existing = await _context.Tenants
                    .AnyAsync(t => t.Domain.ToLower() == ten.Domain.ToLower());

                if (existing)
                    throw new InvalidOperationException($"A tenant with domain '{ten.Domain}' already exists.");

                var tenant = new Tenant
                {
                    Name = ten.Name.Trim(),
                    Domain = ten.Domain.Trim().ToLower(),
                    Email = ten.Email.Trim(),
                    PhoneNumber = ten.PhoneNumber,
                    City = ten.City,
                    Address = ten.Address,
                    TenantUrl = GenerateStringUrl()
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();
                // 3. Tenant ke liye Admin User create karo
                var adminUser = new ApplicationUser
                {
                    UserName = ten.Email,
                    Email = ten.Email,
                    TenantId = tenant.Id,   // yahan Tenant assign kiya
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(adminUser, "Admin@123");

                if (!result.Succeeded)
                    throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

                // 4. Role ensure karo (agar Admin role exist nahi karta to create karo)
                if (_roleManager.Roles.FirstOrDefault(r => r.Name == "Admin" && r.TenantId == tenant.Id) == null)
                {
                    await _roleManager.CreateAsync(new ApplicationRole { Name = "Admin", TenantId = tenant.Id });
                }

                // 5. User ko Admin role do
                var adminRole = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == "Admin" && r.TenantId == tenant.Id);

                if (adminRole == null)
                {
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    throw new Exception("Admin role not found for tenant!");
                }

                return tenant;
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task<Tenant?> UpdateTenantAsync(Tenant ten, Guid? ownerUserId)
        {
            try
            {
                var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == ten.Id);
                if (tenant == null)
                    return null;

                if (!string.IsNullOrWhiteSpace(ten.Name))
                    tenant.Name = ten.Name.Trim();
                if (!string.IsNullOrWhiteSpace(ten.Email))
                    tenant.Email = ten.Email.Trim();
                if (!string.IsNullOrWhiteSpace(ten.City))
                    tenant.City = ten.City;
                if (!string.IsNullOrWhiteSpace(ten.PhoneNumber))
                    tenant.PhoneNumber = ten.PhoneNumber;
                if (!string.IsNullOrWhiteSpace(ten.Address))
                    tenant.Address = ten.Address;

                if (!string.IsNullOrWhiteSpace(ten.Domain))
                {
                    var domainExists = await _context.Tenants
                        .AnyAsync(t => t.Domain.ToLower() == ten.Domain.ToLower() && t.Id != ten.Id);

                    if (domainExists)
                        throw new InvalidOperationException($"A tenant with domain '{ten.Domain}' already exists.");

                    tenant.Domain = ten.Domain.Trim().ToLower();
                }

                _context.Tenants.Update(tenant);
                await _context.SaveChangesAsync();

                return tenant;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while updating the tenant.", ex);
            }
        }

        public async Task<bool> DeleteTenantAsync(Guid id)
        {
            try
            {
                var tenant = await _context.Tenants.FindAsync(id);
                if (tenant == null)
                    return false;

                tenant.IsDeleted = true;
                _context.Tenants.Update(tenant);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while deleting the tenant.", ex);
            }
        }

        private string GenerateStringUrl()
        {
            // Guid → byte[]
            var bytes = Guid.NewGuid().ToByteArray();

            // Base64 → URL Safe
            string base64 = Convert.ToBase64String(bytes)
                .Replace("+", "-")   // replace + with -
                .Replace("/", "_")   // replace / with _
                .Replace("=", "");   // remove padding

            return base64;
        }
    }
}
