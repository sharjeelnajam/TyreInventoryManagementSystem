using Domain;
using Microsoft.EntityFrameworkCore;
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

        public SupplierService(ApplicationDbContext context)
        {
            _context = context;
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

                _context.Supplier.Add(supplier);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
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
    }
}
