using Domain.Enums;
using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Shared.MultiTenancy;

namespace Infrastructure.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public CustomerService(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<Customer>> GetCustomersAsync(string? name = null, CustomerType? type = null)
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    var query = _context.Customer.Where(c => c.TenantId == _tenantProvider.TenantId);

                    if (!string.IsNullOrWhiteSpace(name))
                        query = query.Where(s => s.Name.Contains(name));

                    //if (type.HasValue)
                    //    query = query.Where(c => c.CustomerType == type);

                    var customers = await query.ToListAsync();
                    return customers;
                }
                else return new List<Customer>();

               
            }
            catch (Exception ex)
            {
                // Log exception (replace with your logging framework)
                Console.WriteLine($"Error in GetCustomersAsync: {ex.Message}");
                return new List<Customer>();
            }
        }

        public async Task<Customer?> GetCustomerByIdAsync(Guid id)
        {
            try
            {
                if (_tenantProvider.TenantId != null)
                {
                    return await _context.Customer.FirstOrDefaultAsync(c => c.Id == id && _tenantProvider.TenantId == c.TenantId);
                }
                else return new Customer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCustomerByIdAsync: {ex.Message}");
                return null;
            }
        }

        public async Task AddCustomerAsync(Customer customer)
        {
            try
            {
                _context.Customer.Add(customer);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddCustomerAsync: {ex.Message}");
            }
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            try
            {
                var existingCustomer = await _context.Customer
               .FirstOrDefaultAsync(x => x.Id == customer.Id);

                if (existingCustomer == null)
                    return;

                // Update ONLY editable fields
                existingCustomer.Name = customer.Name;
                existingCustomer.Phone = customer.Phone;
                existingCustomer.Address = customer.Address;
                existingCustomer.City = customer.City;
                existingCustomer.VehicleNumber = customer.VehicleNumber;
                existingCustomer.CreditLimit = customer.CreditLimit;
                existingCustomer.Percentage = customer.Percentage;
                existingCustomer.CustomPrice = customer.CustomPrice;
                existingCustomer.ListManagementId = customer.ListManagementId;
                existingCustomer.IsActive = customer.IsActive;

                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task DeleteCustomerAsync(Guid id)
        {
            try
            {
                var customer = await _context.Customer.FindAsync(id);
                if (customer != null)
                {
                    _context.Customer.Remove(customer); // triggers soft delete logic
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteCustomerAsync: {ex.Message}");
            }
        }
    }
}
