using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly ApplicationDbContext _context;

        public TenantService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Tenant> GetTenantByIdAsync(Guid id)
        {
            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant == null)
                throw new KeyNotFoundException($"Tenant with Id '{id}' not found.");

            return tenant;
        }

        /// <summary>
        /// Get all tenants with owner user details.
        /// </summary>
        public async Task<List<Tenant>> GetTenantsAsync()
        {
            return await _context.Tenants.Where(t => t.IsDeleted == false)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get tenant by domain name.
        /// </summary>
        public async Task<Tenant?> GetTenantByDomainAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            return await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Domain.ToLower() == domain.ToLower());
        }

        /// <summary>
        /// Add new tenant. Prevents duplicate domain names.
        /// </summary>
        public async Task<Tenant> AddTenantAsync(Tenant ten, Guid ownerUserId)
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
                Id = Guid.NewGuid(),
                Name = ten.Name.Trim(),
                Domain = ten.Domain.Trim().ToLower(),
                Email = ten.Email.Trim(),
                PhoneNumber = ten.PhoneNumber,
                City = ten.City,
                Address = ten.Address
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();
            return tenant;
        }

        /// <summary>
        /// Update existing tenant. Null parameters mean "no change".
        /// </summary>
        public async Task<Tenant?> UpdateTenantAsync(Tenant ten, Guid? ownerUserId)
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
                // check duplicate domain (ignore current tenant)
                var exists = await _context.Tenants
                    .AnyAsync(t => t.Domain.ToLower() == ten.Domain.ToLower() && t.Id != ten.Id);

                if (exists)
                    throw new InvalidOperationException($"A tenant with domain '{ten.Domain}' already exists.");

                tenant.Domain = ten.Domain.Trim().ToLower();
            }

            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();
            return tenant;
        }

        /// <summary>
        /// Delete tenant if exists.
        /// </summary>
        public async Task<bool> DeleteTenantAsync(Guid id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
                return false;

            tenant.IsDeleted = true;
            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
