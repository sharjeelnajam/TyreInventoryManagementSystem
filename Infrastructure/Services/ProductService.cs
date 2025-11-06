using Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context; // 👈 Apna DbContext ka naam use karein

        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetAllAsync()
        {
            try
            {
                return await _context.Products.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetAllAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<Product?> GetByIdAsync(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    throw new ArgumentException("Invalid product id.");

                return await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetByIdAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<Product> AddAsync(Product product)
        {
            try
            {
                if (product == null)
                    throw new ArgumentNullException(nameof(product));
                
                product.CreatedAt = DateTime.UtcNow;

                await _context.Products.AddAsync(product);
                await _context.SaveChangesAsync();

                return product;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in AddAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<Product> UpdateAsync(Product product)
        {
            try
            {
                if (product == null || product.Id == Guid.Empty)
                    throw new ArgumentException("Invalid product data.");

                var existing = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == product.Id && !p.IsDeleted);

                if (existing == null)
                    throw new KeyNotFoundException("Product not found or already deleted.");

                // ✅ update fields
                existing.ProductName = product.ProductName;
                existing.Description = product.Description;
                existing.DOT = product.DOT;
                existing.Brand = product.Brand;
                existing.TyreSize = product.TyreSize;
                existing.TreadDepth = product.TreadDepth;
                existing.PurchasePrice = product.PurchasePrice;
                existing.SellingPrice = product.SellingPrice;
                existing.Quantity = product.Quantity;
                existing.Barcode = product.Barcode;

                if (!string.IsNullOrEmpty(product.ImagePath))
                {
                    existing.ImagePath = product.ImagePath;
                }
                // If product.ImagePath is null/empty and you want to allow image removal:
                else if (product.ImagePath == null)
                {
                    existing.ImagePath = null; // This allows removing the image
                }

                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return existing;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in UpdateAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    throw new ArgumentException("Invalid product id.");

                var existing = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

                if (existing == null)
                    return false;

                // ✅ Soft delete
              _context.Products.Remove(existing); // triggers soft delete logic
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in DeleteAsync: {ex.Message}");
                throw;
            }
        }
    }
}
