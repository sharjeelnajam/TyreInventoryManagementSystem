using Domain;
using Domain.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context; 
        private readonly ITenantProvider _tenantProvider;
        private readonly IPurchaseService _purchaseService;

        public ProductService(ApplicationDbContext context, ITenantProvider tenantProvider, IPurchaseService purchaseService)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _purchaseService = purchaseService;
        }
        public async Task<Product> AddAsync(ProductDto productDto)
        {
            try
            {
                if (productDto == null)
                    throw new ArgumentNullException(nameof(productDto));

                var product = DtoToEtity(productDto);

                product.CreatedAt = DateTime.UtcNow;

                await _context.Products.AddAsync(product);
                await _context.SaveChangesAsync();
                if(productDto.Quantity > 0)
                {
                    Purchase purchase = new()
                    {
                        SupplierId = null,
                        PurchaseDate = DateTime.Now,
                        PaymentMethod = string.Empty,
                        PaymentStatus = string.Empty,
                        TotalAmount = productDto.PurchasePrice * productDto.Quantity,
                        NetAmount = productDto.PurchasePrice * productDto.Quantity,
                        PurchaseNumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}",
                        PurchaseDetails = new List<PurchaseDetail>
                    {
                        new PurchaseDetail
                        {
                            UnitPrice = productDto.PurchasePrice,
                            SellingPrice = productDto.SellingPrice,
                            TotalPrice = productDto.PurchasePrice * productDto.Quantity,
                            ProductId = product.Id,
                            Quantity = productDto.Quantity,
                            Brand = productDto.Brand,
                        }
                    }
                    };

                    await _purchaseService.AddPurchaseAsync(purchase);
                }
                
                return product;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in AddAsync: {ex.Message}");
                throw;
            }
        }

        public async  Task<List<ProductDto>> GetAllAsync()
        {
            try
            {
                List<ProductDto> productList = [.. _context.Products
                   .Where(p => p.TenantId == _tenantProvider.TenantId)
                   .Include(p => p.PurchaseDetails)
                   .Select(product => new ProductDto
                   {
                       Id = product.Id,
                       Barcode = product.Barcode,
                       Brand = product.Brand,
                       ProductName = product.ProductName,
                       Min_Threshold = product.Min_Threshold,
                       Thread = product.Thread,
                       AverageCostPrice = product.AverageCostPrice,
                       ImagePath = product.ImagePath,
                       Description = product.Description,
                       DOT = product.DOT,
                       TyreSize = product.TyreSize,
                       Type = product.Type,
                       Size = product.Size,
                       Unit = product.Unit,

                       PurchasePrice = product.PurchaseDetails
                            .OrderByDescending(pd => pd.Id)
                            .Select(pd => pd.UnitPrice)
                            .FirstOrDefault(),

                         SellingPrice = product.PurchaseDetails
                            .OrderByDescending(pd => pd.Id)
                            .Select(pd => pd.SellingPrice)
                            .FirstOrDefault(),

                       Quantity =  _context.StockHistories
                            .Where(s => s.ProductId == product.Id)
                            .Select(s => s.NewStockLevel)
                            .FirstOrDefault()
                   })
                   .AsNoTracking()];


                return productList;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<List<ProductDto>> GetAllBranchesProducts()
        {
            try
            {
                List<ProductDto> productList = [.. _context.Products
                   .Include(p => p.PurchaseDetails)
                   .Select(product => new ProductDto
                   {
                       Id = product.Id,
                       Barcode = product.Barcode,
                       Brand = product.Brand,
                       ProductName = product.ProductName,
                       Min_Threshold = product.Min_Threshold,
                       Thread = product.Thread,
                       AverageCostPrice = product.AverageCostPrice,
                       ImagePath = product.ImagePath,
                       Description = product.Description,
                       DOT = product.DOT,
                       TyreSize = product.TyreSize,
                       Type = product.Type,

                       PurchasePrice = product.PurchaseDetails
                            .OrderByDescending(pd => pd.Id)
                            .Select(pd => pd.UnitPrice)
                            .FirstOrDefault(),

                         SellingPrice = product.PurchaseDetails
                            .OrderByDescending(pd => pd.Id)
                            .Select(pd => pd.SellingPrice)
                            .FirstOrDefault(),

                       Quantity =  _context.StockHistories
                            .Where(s => s.ProductId == product.Id)
                            .Select(s => s.NewStockLevel)
                            .FirstOrDefault()
                   })
                   .AsNoTracking()];


                return productList;
            }
            catch (Exception)
            {

                throw;
            }
        }

        //public async Task<List<Product>> GetAllAsync()
        //{
        //    try
        //    {
        //        if (_tenantProvider.TenantId != Guid.Empty)
        //        {
        //            return await _context.Products.Where(p => p.TenantId == _tenantProvider.TenantId).AsNoTracking().ToListAsync();

        //        }
        //        return new List<Product>();

               
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error in GetAllAsync: {ex.Message}");
        //        throw;
        //    }
        //}

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    throw new ArgumentException("Invalid product id.");

                //return await _context.Products.AsNoTracking().Include(p => p.PurchaseDetails).FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _tenantProvider.TenantId);

                var product = await _context.Products.AsNoTracking().Include(p => p.PurchaseDetails).FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _tenantProvider.TenantId);
               return await EntityToDto(product);
            }


            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetByIdAsync: {ex.Message}");
                throw;
            }
        }

        //public async Task<ProductDto> UpdateAsync(ProductDto product)
        //{
        //    try
        //    {
        //        if (product == null || product.Id == Guid.Empty)
        //            throw new ArgumentException("Invalid product data.");

        //        var existing = await _context.Products
        //            .FirstOrDefaultAsync(p => p.Id == product.Id && !p.IsDeleted);

        //        if (existing == null)
        //            throw new KeyNotFoundException("Product not found or already deleted.");

        //        // ✅ update fields
        //        existing.ProductName = product.ProductName;
        //        existing.Description = product.Description;
        //        existing.DOT = product.DOT;
        //        existing.Brand = product.Brand;
        //        existing.Type = product.Type;
        //        existing.Min_Threshold = product.Min_Threshold;
        //        existing.Thread = product.Thread;
        //        existing.TyreSize = product.TyreSize;
        //        existing.Barcode = product.Barcode;
        //        existing.AverageCostPrice = product.AverageCostPrice;
        //        existing.UpdatedAt = DateTime.UtcNow;


        //        if (!string.IsNullOrEmpty(product.ImagePath))
        //        {
        //            existing.ImagePath = product.ImagePath;
        //        }
        //        // If product.ImagePath is null/empty and you want to allow image removal:
        //        else if (product.ImagePath == null)
        //        {
        //            existing.ImagePath = null; // This allows removing the image
        //        }

        //        var purchaseDetail = _context.PurchaseDetails.FirstOrDefault(pd => pd.ProductId == product.Id);

        //        if (purchaseDetail != null)
        //        {
        //            var purchase = _context.Purchase.FirstOrDefault(p => p.Id == purchaseDetail.PurchaseId);

        //            purchase.UpdatedAt = DateTime.Now;
        //            purchase.TotalAmount = product.PurchasePrice * product.Quantity;
        //            purchase.NetAmount = product.PurchasePrice * product.Quantity;
        //            purchase.PurchaseDetails.unitPrice = product.PurchasePrice;
        //            purchase.PurchaseDetails.SellingPrice = product.PurchasePrice;
        //            purchase.PurchaseDetails.TotalPrice = product.PurchasePrice * product.Quantity;
        //            purchase.PurchaseDetails.Quantity = product.Quantity;
        //            purchase.PurchaseDetails.Brand = product.Brand;


        //        }

        //        await _context.SaveChangesAsync();

        //        return existing;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error in UpdateAsync: {ex.Message}");
        //        throw;
        //    }
        //}
        public async Task<ProductDto> UpdateAsync(ProductDto dto)
        {
            try
            {
                if (dto == null || dto.Id == Guid.Empty)
                    throw new ArgumentException("Invalid product data.");

                // 1️⃣ UPDATE PRODUCT
                var existing = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == dto.Id && !p.IsDeleted);

                if (existing == null)
                    throw new KeyNotFoundException("Product not found.");

                existing.ProductName = dto.ProductName;
                existing.Description = dto.Description;
                existing.DOT = dto.DOT;
                existing.Brand = dto.Brand;
                existing.Type = dto.Type;
                existing.Min_Threshold = dto.Min_Threshold;
                existing.Thread = dto.Thread;
                existing.TyreSize = dto.TyreSize;
                existing.Barcode = dto.Barcode;
                existing.AverageCostPrice = dto.AverageCostPrice;
                existing.Unit = dto.Unit; ;
                existing.Size = dto.Size;
                existing.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(dto.ImagePath))
                    existing.ImagePath = dto.ImagePath;
                else if (dto.ImagePath == null)
                    existing.ImagePath = null;

                // 2️⃣ GET PURCHASE & DETAILS
                var purchaseDetail = await _context.PurchaseDetails
                    .FirstOrDefaultAsync(pd => pd.ProductId == dto.Id);

                if (purchaseDetail != null)
                {
                    var purchase = await _context.Purchase
                        .FirstOrDefaultAsync(p => p.Id == purchaseDetail.PurchaseId);

                    if (purchase != null)
                    {
                        // 3️⃣ UPDATE PURCHASE FIELDS
                        purchase.UpdatedAt = DateTime.UtcNow;
                        purchase.TotalAmount = dto.PurchasePrice * dto.Quantity;
                        purchase.NetAmount = dto.PurchasePrice * dto.Quantity;

                        // 4️⃣ UPDATE PURCHASE DETAIL FIELDS
                        purchaseDetail.UnitPrice = dto.PurchasePrice;
                        purchaseDetail.SellingPrice = dto.SellingPrice;
                        purchaseDetail.TotalPrice = dto.PurchasePrice * dto.Quantity;
                        purchaseDetail.Quantity = dto.Quantity;
                        purchaseDetail.Brand = dto.Brand;
                    }
                }

                // 5️⃣ SAVE CHANGES
                await _context.SaveChangesAsync();

                return await EntityToDto(existing);
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

        private Product DtoToEtity(ProductDto productDto)
        {
            try
            {
                Product product = new()
                {
                    ProductName = productDto.ProductName,
                    Description = productDto.Description,
                    Brand = productDto.Brand,
                    DOT = productDto.DOT,
                    TyreSize = productDto.TyreSize,
                    Min_Threshold = productDto.Min_Threshold,
                    Type = productDto.Type,
                    Thread = productDto.Thread,
                    AverageCostPrice = productDto.AverageCostPrice,
                    Barcode = productDto.Barcode,
                    ImagePath = productDto.ImagePath,
                    Unit = productDto.Unit,
                    Size = productDto.Size
                };
                return product;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<ProductDto?> EntityToDto(Product? product)
        {
            if (product == null) return null;

            var productDto = new ProductDto
            {
                Barcode = product.Barcode,
                Brand = product.Brand,
                ProductName = product.ProductName,
                Min_Threshold = product.Min_Threshold,
                Thread = product.Thread,
                AverageCostPrice = product.AverageCostPrice,
                ImagePath = product.ImagePath,
                Description = product.Description,
                DOT = product.DOT,
                TyreSize = product.TyreSize,
                Type = product.Type,
                Unit = product.Unit,
                Size = product.Size,

                PurchasePrice = product.PurchaseDetails
                    .OrderByDescending(pd => pd.Id) 
                    .Select(pd => pd.UnitPrice)
                    .FirstOrDefault(),

                SellingPrice = product.PurchaseDetails
                    .OrderByDescending(pd => pd.Id)
                    .Select(pd => pd.SellingPrice)
                    .FirstOrDefault()
            };

            productDto.Quantity = await _context.StockHistories
                .Where(s => s.ProductId == product.Id)
                .Select(s => s.NewStockLevel)
                .FirstOrDefaultAsync();

            return productDto;
        }
    }
}
