using Domain.Enums;
using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Infrastructure.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;

        public CustomerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Customer>> GetCustomersAsync(string? name = null, CustomerType? type = null)
        {
            try
            {
                var query = _context.Customer.AsQueryable();

                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(s => s.Name.Contains(name));

                if (type.HasValue)
                    query = query.Where(c => c.CustomerType == type);

                return await query.ToListAsync();
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
                return await _context.Customer.FirstOrDefaultAsync(c => c.Id == id);
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
            var existingCustomer = await _context.Customer.FindAsync(customer.Id);

            if (existingCustomer != null)
            {
                // Update only the properties you want to change
                _context.Entry(existingCustomer).CurrentValues.SetValues(customer);

                await _context.SaveChangesAsync();
            }
            else
            {
                Console.WriteLine("Customer not found.");
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
