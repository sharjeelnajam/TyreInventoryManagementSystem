using Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class SaleService : ISaleService
    {
        private readonly ApplicationDbContext _context;

        public SaleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Sale>> GetAllSalesAsync()
        {
            try
            {
                return await _context.Sale
                   .Include(s => s.Customer)
                   .Include(s => s.SaleDetails)
                   .Where(s => !s.IsDeleted)
                   .OrderByDescending(s => s.CreatedAt)
                   .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            try
            {
                return await _context.Sale.AnyAsync(s => s.Id == id);
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<Sale?> GetSaleByIdAsync(Guid id)
        {
            try
            {
                return await _context.Sale
               .Include(s => s.SaleDetails)
               .FirstOrDefaultAsync(s => s.Id == id);
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task AddSaleAsync(Sale sale)
        {
            try
            {
                NormalizeSale(sale);

                sale.Id = sale.Id == Guid.Empty ? Guid.NewGuid() : sale.Id;
                sale.CreatedAt = DateTime.UtcNow;

                _context.Sale.Add(sale);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task UpdateSaleAsync(Sale sale)
        {
            try
            {
                var existing = await _context.Sale
             .Include(s => s.SaleDetails)
             .FirstOrDefaultAsync(s => s.Id == sale.Id);

                if (existing == null) return;

                // Update main fields
                _context.Entry(existing).CurrentValues.SetValues(sale);

                // Delete removed details
                var detailIds = sale.SaleDetails?.Select(d => d.Id).ToList() ?? new List<Guid>();
                var toRemove = existing.SaleDetails.Where(d => !detailIds.Contains(d.Id)).ToList();
                foreach (var r in toRemove)
                    _context.SaleDetail.Remove(r);

                // Add or update details
                foreach (var detail in sale.SaleDetails)
                {
                    var existingDetail = existing.SaleDetails.FirstOrDefault(d => d.Id == detail.Id);

                    if (existingDetail != null)
                    {
                        _context.Entry(existingDetail).CurrentValues.SetValues(detail);
                        existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;
                    }
                    else
                    {
                        detail.Id = Guid.NewGuid();
                        detail.SaleId = sale.Id;
                        detail.TotalPrice = detail.Quantity * detail.UnitPrice;
                        existing.SaleDetails.Add(detail);
                    }
                }

                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        private void NormalizeSale(Sale sale)
        {
            try
            {
                foreach (var d in sale.SaleDetails)
                {
                    if (d.Id == Guid.Empty)
                        d.Id = Guid.NewGuid();

                    d.SaleId = sale.Id;
                    d.TotalPrice = d.Quantity * d.UnitPrice;
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        // Dummy helpers – replace with actual repo/services
        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                return await _context.Customer.ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                return await _context.Products.ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task DeleteSaleAsync(Guid id)
        {
            try
            {
                var sale = await _context.Sale
               .Include(s => s.SaleDetails)
               .FirstOrDefaultAsync(s => s.Id == id);

                if (sale == null) return;

                sale.IsDeleted = true;
                sale.DeletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }
    }
}
