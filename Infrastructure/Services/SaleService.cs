using Domain;
using Domain.DTO;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Shared.MultiTenancy;
using System.Security.Claims;

namespace Infrastructure.Services
{
    public class SaleService : ISaleService
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ITenantProvider _tenantProvider;

        public SaleService(ApplicationDbContext context, IServiceScopeFactory scopeFactory, AuthenticationStateProvider authenticationStateProvider, ITenantProvider tenantProvider)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _authStateProvider = authenticationStateProvider;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<Sale>> GetAllAsync()
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    var sales = await _context.Sale
                        .Where(s => s.TenantId == _tenantProvider.TenantId)
                   .Include(s => s.SaleDetails)
                       .ThenInclude(sd => sd.Product)
                   .ToListAsync();

                    // Manually load and attach customers
                    await HydrateSalesWithCustomers(sales);

                    return sales ?? new List<Sale>();
                }
                else return new List<Sale>();
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetAllAsync: {ex.Message}");
                return new List<Sale>();
            }
        }

        public async Task<Sale?> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return null;

            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    var sale = await _context.Sale
                           .Include(s => s.SaleDetails)
                               .ThenInclude(sd => sd.Product)
                           .AsNoTracking()
                           .FirstOrDefaultAsync(s => s.Id == id);

                    if (sale == null) return null;

                    // Manually load and attach customer
                    await HydrateSaleWithCustomer(sale);

                    return sale;

                }
                else return new Sale();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetByIdAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AddAsync(Sale sale)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (sale == null) return false;

                if (sale.CustomerId == Guid.Empty)
                    sale.CustomerId = null;

                if (string.IsNullOrWhiteSpace(sale.PaymentMethod))
                    sale.PaymentMethod = "Cash";
                if (string.IsNullOrWhiteSpace(sale.PaymentStatus))
                    sale.PaymentStatus = "Paid";

                sale.Id = Guid.NewGuid();
                sale.CreatedAt = DateTime.Now;
                if (_tenantProvider.TenantId != Guid.Empty)
                    sale.TenantId = _tenantProvider.TenantId;

                List<SaleDetail> saleDetails = new List<SaleDetail>();

                if (sale.SaleDetails != null)
                {
                    foreach (var detail in sale.SaleDetails)
                    {
                        detail.SaleId = sale.Id;
                        detail.TotalPrice = detail.Quantity * detail.UnitPrice;
                        if (_tenantProvider.TenantId != Guid.Empty)
                            detail.TenantId = _tenantProvider.TenantId;

                        // NEW: Fetch cost price & calculate profit
                        var product = await _context.Products.FindAsync(detail.ProductId);
                        if (product != null)
                        {
                            detail.CostPrice = product.AverageCostPrice;
                            detail.ProfitAmount = Math.Round((detail.UnitPrice - detail.CostPrice) * detail.Quantity, 2);
                        }

                        saleDetails.Add(detail);
                    }
                }

                await _context.Sale.AddAsync(sale);
                if (saleDetails.Any())
                    _context.AddRange(saleDetails);

                await _context.SaveChangesAsync();

                // Update stock and history
                if (sale.SaleDetails != null && sale.SaleDetails.Any())
                {
                    var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
                    var currentUser = authState.User;
                    var performedBy = currentUser.FindFirst(ClaimTypes.Name)?.Value ??
                                      currentUser.FindFirst("name")?.Value ??
                                      currentUser.Identity?.Name;

                    foreach (var detail in sale.SaleDetails)
                    {
                        // Use latest stock record for this product (by ActionDate then Id) so we update current stock
                        var stockProduct = await _context.StockHistories
                            .Where(p => p.ProductId == detail.ProductId)
                            .OrderByDescending(sh => sh.ActionDate)
                            .ThenByDescending(sh => sh.Id)
                            .FirstOrDefaultAsync();
                        var product = await _context.Products.FindAsync(detail.ProductId);

                        if (stockProduct != null)
                        {
                            var previousLevel = stockProduct.NewStockLevel;
                            stockProduct.NewStockLevel = stockProduct.NewStockLevel - detail.Quantity;
                            stockProduct.QuantityChanged = -detail.Quantity;
                            stockProduct.ActionDate = DateTime.UtcNow;
                            stockProduct.PerformedBy = performedBy;
                            stockProduct.ReferenceId = sale.Id;
                            stockProduct.ReferenceNumber = sale.SaleNumber;
                            stockProduct.ActionType = "Sale";
                            stockProduct.PreviousStockLevel = previousLevel;
                            _context.Update(stockProduct);

                            // Add a new StockHistory row for this sale (audit trail)
                            var saleStockHistory = new StockHistory
                            {
                                Id = Guid.NewGuid(),
                                ProductId = detail.ProductId,
                                ActionType = "Sale",
                                QuantityChanged = -detail.Quantity,
                                PreviousStockLevel = previousLevel,
                                NewStockLevel = stockProduct.NewStockLevel,
                                ActionDate = DateTime.UtcNow,
                                ReferenceId = sale.Id,
                                ReferenceNumber = sale.SaleNumber,
                                PerformedBy = performedBy,
                                TenantId = _tenantProvider.TenantId
                            };
                            await _context.StockHistories.AddAsync(saleStockHistory);
                        }

                        // NEW: Add ProfitHistory record
                        if (product != null)
                        {
                            var profitHistory = new ProfitHistory
                            {
                                SaleId = sale.Id,
                                SaleDetailId = detail.Id,
                                ProductId = detail.ProductId,
                                CostPrice = detail.CostPrice,
                                SellingPrice = detail.UnitPrice,
                                Quantity = detail.Quantity,
                                ProfitAmount = detail.ProfitAmount,
                                RecordedAt = sale.CreatedAt,
                                PerformedBy = performedBy,
                                ReferenceNumber = sale.SaleNumber
                            };
                            _context.ProfitHistories.Add(profitHistory);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error in AddAsync: {ex.Message}");
                return false;
            }
        }

        public async Task UpdateAsync(Sale sale)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            try
            {
                var existingSales = await ctx.Sale.Include(p => p.SaleDetails).FirstOrDefaultAsync(p => p.Id == sale.Id);

                if (existingSales == null)
                    throw new Exception("Purchase not found in DB");

                // Update fields
                existingSales.SaleNumber = sale.SaleNumber;
                existingSales.CustomerId = sale.CustomerId;
                existingSales.TotalAmount = sale.TotalAmount;
                existingSales.Discount = sale.Discount;
                existingSales.TaxAmount = sale.TaxAmount;
                existingSales.NetAmount = sale.NetAmount;
                existingSales.PaymentMethod = sale.PaymentMethod;
                existingSales.PaymentStatus = sale.PaymentStatus;
                existingSales.DueDate = sale.DueDate;
                existingSales.VehicleNumber = sale.VehicleNumber;
                existingSales.IsApproved = sale.IsApproved;
                existingSales.UpdatedAt = DateTime.Now; // best practice

                // --- Sync SaleDetails ---
                var updatedDetailIds = sale.SaleDetails?.Select(s => s.Id).ToList() ?? new List<Guid>();

                var toRemove = ctx.SaleDetail.Where(s => !updatedDetailIds.Contains(s.Id) && s.SaleId == sale.Id).ToList();
                var availAbleStocks = ctx.StockHistories.ToList();

                foreach (var r in toRemove)
                {
                    var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == r.ProductId);

                    if (stockProduct != null)
                    {
                        stockProduct.NewStockLevel = stockProduct.NewStockLevel + r.Quantity;
                        ctx.StockHistories.Update(stockProduct);
                    }

                    ctx.SaleDetail.Remove(r);
                }
                await ctx.SaveChangesAsync();

                // Add or Update
                if (sale.SaleDetails != null && sale.SaleDetails.Any())
                {
                    foreach (var detail in sale.SaleDetails)
                    {
                        var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        var existingDetail = existingSales.SaleDetails.FirstOrDefault(s => s.Id == detail.Id);

                        if (existingDetail != null && stockProduct != null)
                        {
                            if (detail.Quantity < existingDetail.Quantity)
                            {
                                int newValue = existingDetail.Quantity - detail.Quantity;

                                existingDetail.ProductId = detail.ProductId;
                                existingDetail.Quantity = detail.Quantity;
                                existingDetail.UnitPrice = detail.UnitPrice;
                                existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;

                                stockProduct.NewStockLevel = stockProduct.NewStockLevel + newValue;
                                stockProduct.UpdatedAt = DateTime.Now;
                                ctx.StockHistories.Update(stockProduct);
                            }
                            else if (detail.Quantity > existingDetail.Quantity)
                            {
                                var newValue = detail.Quantity - existingDetail.Quantity;

                                existingDetail.ProductId = detail.ProductId;
                                existingDetail.Quantity = detail.Quantity;
                                existingDetail.UnitPrice = detail.UnitPrice;
                                existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;

                                stockProduct.NewStockLevel = stockProduct.NewStockLevel - newValue;
                                stockProduct.UpdatedAt = DateTime.Now;
                                ctx.StockHistories.Update(stockProduct);
                            }
                            else
                            {
                                // Quantity same — update price and total only
                                existingDetail.UnitPrice = detail.UnitPrice;
                                existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;
                            }

                            ctx.SaleDetail.Update(existingDetail);

                            // 🟢 NEW: Update ProfitHistory record for edited detail
                            var profit = await ctx.ProfitHistories.FirstOrDefaultAsync(p => p.SaleDetailId == existingDetail.Id);
                            if (profit != null)
                            {
                                var product = await ctx.Products.FindAsync(detail.ProductId);
                                existingDetail.CostPrice = product.AverageCostPrice;
                                existingDetail.ProfitAmount = Math.Round((detail.UnitPrice - existingDetail.CostPrice) * detail.Quantity, 2);

                                profit.CostPrice = existingDetail.CostPrice;
                                profit.SellingPrice = existingDetail.UnitPrice;
                                profit.Quantity = existingDetail.Quantity;
                                profit.ProfitAmount = existingDetail.ProfitAmount;
                                profit.RecordedAt = DateTime.Now;

                                ctx.ProfitHistories.Update(profit);
                            }
                            else
                            {
                                var product = await ctx.Products.FindAsync(detail.ProductId);
                                var ph = new ProfitHistory
                                {
                                    SaleId = existingSales.Id,
                                    SaleDetailId = detail.Id,
                                    ProductId = detail.ProductId,
                                    CostPrice = product.AverageCostPrice,
                                    SellingPrice = detail.UnitPrice,
                                    Quantity = detail.Quantity,
                                    ProfitAmount = Math.Round((detail.UnitPrice - product.AverageCostPrice) * detail.Quantity, 2),
                                    RecordedAt = DateTime.Now,
                                    ReferenceNumber = existingSales.SaleNumber
                                };
                                ctx.ProfitHistories.Add(ph);
                            }
                            // 🟢 END NEW
                        }
                        else
                        {
                            // Add new
                            if (detail.Id == Guid.Empty)
                                detail.Id = Guid.NewGuid();

                            detail.SaleId = existingSales.Id;
                            detail.TotalPrice = detail.Quantity * detail.UnitPrice;

                            stockProduct.NewStockLevel = stockProduct.NewStockLevel - detail.Quantity;
                            ctx.StockHistories.Update(stockProduct);
                            stockProduct.UpdatedAt = DateTime.Now;

                            ctx.SaleDetail.Add(detail);

                            // 🟢 NEW: Add new ProfitHistory + StockHistory entries for new details
                            var product = await ctx.Products.FindAsync(detail.ProductId);
                            detail.CostPrice = product.AverageCostPrice;
                            detail.ProfitAmount = Math.Round((detail.UnitPrice - product.AverageCostPrice) * detail.Quantity, 2);

                            var phNew = new ProfitHistory
                            {
                                SaleId = existingSales.Id,
                                SaleDetailId = detail.Id,
                                ProductId = detail.ProductId,
                                CostPrice = detail.CostPrice,
                                SellingPrice = detail.UnitPrice,
                                Quantity = detail.Quantity,
                                ProfitAmount = detail.ProfitAmount,
                                RecordedAt = DateTime.Now,
                                ReferenceNumber = existingSales.SaleNumber
                            };
                            ctx.ProfitHistories.Add(phNew);

                            var shNew = new StockHistory
                            {
                                ProductId = detail.ProductId,
                                ActionType = "Sale",
                                QuantityChanged = -detail.Quantity,
                                PreviousStockLevel = stockProduct.NewStockLevel + detail.Quantity,
                                NewStockLevel = stockProduct.NewStockLevel,
                                ReferenceId = existingSales.Id,
                                ReferenceNumber = existingSales.SaleNumber,
                                ActionDate = DateTime.Now
                            };
                            ctx.StockHistories.Add(shNew);
                            // 🟢 END NEW
                        }
                    }
                }

                await ctx.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine("⚠️ Concurrency error: " + ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Update error: " + ex.Message);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return false;

            try
            {
                var sale = await _context.Sale.FindAsync(id);
                if (sale == null) return false;

                _context.Sale.Remove(sale);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in DeleteAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SaleDetail>> GetSaleDetailsByProductIdAsync(Guid productId)
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    var saleDetails = await _context.SaleDetail
                   .Include(sd => sd.Sale)
                   .Where(sd => sd.ProductId == productId && sd.TenantId == _tenantProvider.TenantId)
                   .OrderByDescending(sd => sd.Sale.SaleDate)
                   .ToListAsync();

                    // Manually hydrate customers for all related sales
                    await HydrateSalesWithCustomers(saleDetails.Select(sd => sd.Sale).ToList());

                    return saleDetails;
                }
                else return new List<SaleDetail>();


            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetSaleDetailsByProductIdAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<byte[]> GenerateReceiptPdfAsync(Guid saleId)
        {
            var sale = await _context.Sale
                .Include(x => x.SaleDetails)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == saleId);
            // Manually load and attach customer
            await HydrateSaleWithCustomer(sale);

            if (sale == null)
                return Array.Empty<byte>();

            // Resolve logo path – try multiple locations (dev run, publish, different working dirs)
            var logoPath = ResolveWwwRootPath("uploads", "logo", "logo.png");
            byte[]? logoData = !string.IsNullOrEmpty(logoPath) && File.Exists(logoPath) ? await File.ReadAllBytesAsync(logoPath) : null;

            QuestPDF.Settings.License = LicenseType.Community;

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(0);

                    // Optional background image
                    var backgroundPath = ResolveWwwRootPath("uploads", "logo", "backgroundImage.png");
                    if (!string.IsNullOrEmpty(backgroundPath) && File.Exists(backgroundPath))
                    {
                        var bgImage = File.ReadAllBytes(backgroundPath);
                        page.Background().Image(bgImage).FitWidth().FitHeight();
                    }

                    // 🧾 HEADER – logo from wwwroot only (no direct OH&H text)
                    page.Header().Column(col =>
                    {
                        if (logoData != null)
                        {
                            col.Item().PaddingTop(20).AlignCenter().Width(200).Image(logoData).FitArea();
                        }
                        else
                        {
                            col.Item().PaddingTop(20).Height(60);
                        }

                        // Invoice number (bound to sale)
                        col.Item().PaddingTop(24).PaddingLeft(40).Column(invoiceCol =>
                        {
                            invoiceCol.Item().Text("Invoice").FontSize(16).Bold();
                            invoiceCol.Item().Text($"Invoice #: {sale.SaleNumber ?? "N/A"}").FontSize(14);
                        });

                        // Date, Sold To: Name, Email (formatted)
                        col.Item().PaddingTop(16).PaddingLeft(40).PaddingBottom(24).Column(customerCol =>
                        {
                            customerCol.Item().Text($"Date: {sale.SaleDate:dd MMM yyyy}").FontSize(14);
                            customerCol.Item().PaddingTop(8).Text("Sold To:").Bold().FontSize(14);
                            if (sale.Customer != null)
                            {
                                customerCol.Item().Text($"Name: {sale.Customer.Name ?? "N/A"}").FontSize(14);
                                customerCol.Item().Text($"Email: {sale.Customer.Email ?? "N/A"}").FontSize(14);
                            }
                            else
                            {
                                customerCol.Item().Text("Name: N/A").FontSize(14);
                                customerCol.Item().Text("Email: N/A").FontSize(14);
                            }
                        });
                    });

                    // 🧾 CONTENT
                    page.Content().PaddingLeft(40).Column(contentCol =>
                    {
                        // Table
                        contentCol.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().PaddingBottom(5).Text("Description").Bold().FontSize(14);
                                header.Cell().PaddingBottom(5).Text("Quantity").Bold().FontSize(14);
                                header.Cell().PaddingBottom(5).Text("Unit Price").Bold().FontSize(14);
                                header.Cell().PaddingBottom(5).Text("Total").Bold().FontSize(14);
                                header.Cell().ColumnSpan(4)
                                             .PaddingTop(4)
                                             .PaddingBottom(4)
                                             .LineHorizontal(1)
                                             .LineColor(QuestPDF.Helpers.Colors.Black);
                            });

                            foreach (var item in sale.SaleDetails)
                            {
                                table.Cell().PaddingVertical(3).Text(item.Product?.ProductName ?? "N/A").FontSize(14);
                                table.Cell().PaddingVertical(3).Text(item.Quantity.ToString()).FontSize(14);
                                table.Cell().PaddingVertical(3).Text($"£{item.UnitPrice:0.00}").FontSize(14);
                                table.Cell().PaddingVertical(3).Text($"£{item.TotalPrice:0.00}").FontSize(14);
                            }
                        });

                        // Totals (no VAT, no Van registration)
                        contentCol.Item().PaddingTop(24).Column(totalsCol =>
                        {
                            totalsCol.Item().AlignLeft().Text($"SubTotal: £{sale.TotalAmount:0.00}").FontSize(14);
                            totalsCol.Item().AlignLeft().Text($"Total Amount Due: £{sale.NetAmount:0.00}").Bold().FontSize(14);
                        });

                        // Payment Info
                        contentCol.Item().PaddingTop(40).Column(paymentCol =>
                        {
                            paymentCol.Item().Text("Payment Terms: Due on Receipt").FontSize(14);
                            paymentCol.Item().Text($"Payment Method: {sale.PaymentMethod ?? "N/A"}").FontSize(14);
                        });

                        // Thank you – centered
                        contentCol.Item().PaddingTop(30).AlignCenter().Column(thankYouCol =>
                        {
                            thankYouCol.Item().PaddingVertical(10).Text("Thank you for your business!").Italic().FontSize(20);
                        });
                    });

                    // 🧾 FOOTER
                    page.Footer().Row(row =>
                    {
                        // Left
                        row.RelativeItem().PaddingLeft(10).AlignLeft().Text("© Location").FontSize(12);

                        // Center
                        row.RelativeItem().AlignCenter().Column(centerCol =>
                        {
                            centerCol.Item().Text("H&H").Bold().FontSize(10);
                            centerCol.Item().Text("123 Business Street, London, UK, WC1A 1AB").FontSize(9);
                            centerCol.Item().Text("A company of London Holdings Ltd, Vehicle Solutions Company Inc.").FontSize(9);
                        });

                        // Right
                        row.RelativeItem().PaddingRight(20).AlignRight().Text("Phone: +44 20 1234 5678").FontSize(12);
                    });
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }

        public async Task<List<TopProductDto>> GetTopSellingProductsByDateAsync(DateTime start, DateTime end)
        {
            if (_tenantProvider.TenantId != Guid.Empty)
            {
                return await _context.SaleDetail
              .Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.TenantId == _tenantProvider.TenantId)
              .GroupBy(x => new { x.ProductId, x.Product.ProductName })
              .Select(g => new TopProductDto
              {
                  ProductName = g.Key.ProductName,
                  Quantity = g.Sum(x => x.Quantity)
              })
              .OrderByDescending(x => x.Quantity)
              .Take(5)
              .ToListAsync();
            }
            else return new List<TopProductDto>();
              
        }

        public async Task<List<TopCustomerDto>> GetTopCustomersByDateAsync(DateTime start, DateTime end)
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    // 1. Load sales with sale details only
                    var sales = await _context.Sale
                    .Where(s => s.SaleDate >= start && s.SaleDate <= end && s.TenantId == _tenantProvider.TenantId)
                    .Include(s => s.SaleDetails)
                    .ToListAsync();

                    // 2. Hydrate customers manually
                    foreach (var sale in sales)
                    {
                        await HydrateSaleWithCustomer(sale);
                    }

                    // 3. Group in memory (since Customer is hydrated)
                    var result = sales
                       .GroupBy(s => s.Customer?.Name ?? "----")
                       .Select(g => new TopCustomerDto
                       {
                           CustomerName = g.Key,
                           WholesalerName = g.Key,
                           TotalOrders = g.Count(),
                           TotalQuantity = g.SelectMany(x => x.SaleDetails).Sum(x => x.Quantity),
                           TotalAmountSpent = g.Sum(x => x.NetAmount)
                       })
                        .OrderByDescending(x => x.TotalAmountSpent)
                        .Take(5)
                        .ToList();

                    return result;
                }
                else return new List<TopCustomerDto>();
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<List<TopProductDetailDto>> GetTopProductsByDateAsync(DateTime start, DateTime end)
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    return await _context.SaleDetail
                      .Where(i => i.Sale.SaleDate >= start && i.Sale.SaleDate <= end && i.TenantId == _tenantProvider.TenantId)
                      .Include(i => i.Sale)
                      .Include(i => i.Product)
                      .GroupBy(i => new
                      {
                          i.ProductId,
                          i.Product.ProductName,
                          BrandName = i.Product.Brand
                      })
                      .Select(g => new TopProductDetailDto
                      {
                          ProductName = g.Key.ProductName,
                          BrandName = g.Key.BrandName,
                          TotalOrders = g.Select(x => x.SaleId).Distinct().Count(),
                          TotalQuantity = g.Sum(x => x.Quantity),
                          TotalAmount = g.Sum(x => x.TotalPrice)
                      })
                      .OrderByDescending(x => x.TotalQuantity)
                      .Take(5)
                      .ToListAsync();
                }
                else return new List<TopProductDetailDto>();

            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task<int> GetTotalInvoicesAsync()
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    return await _context.Sale.Where(s => s.TenantId == _tenantProvider.TenantId).CountAsync();
                }
                else return 0;
            }
            catch (Exception)
            {

                throw;
            }
          
        }

        public async Task<List<Sale>> GetSalesByCustomerIdAsync(Guid customerId)
        {
            if (customerId == Guid.Empty || _tenantProvider.TenantId == Guid.Empty)
                return new List<Sale>();

            var sales = await _context.Sale
                .Where(s => s.TenantId == _tenantProvider.TenantId && s.CustomerId == customerId)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                .OrderByDescending(s => s.SaleDate)
                .ThenByDescending(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            await HydrateSalesWithCustomers(sales);
            return sales;
        }

        private async Task HydrateSalesWithCustomers(List<Sale> sales)
        {
            var customerIds = sales.Where(s => s.CustomerId.HasValue)
                                  .Select(s => s.CustomerId.Value)
                                  .Distinct()
                                  .ToList();

            if (!customerIds.Any()) return;

            var customers = await _context.Customer
                .Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            foreach (var sale in sales)
            {
                if (sale.CustomerId.HasValue && customers.TryGetValue(sale.CustomerId.Value, out var customer))
                {
                    sale.Customer = customer;
                }
            }
        }

        private async Task HydrateSaleWithCustomer(Sale sale)
        {
            if (sale.CustomerId.HasValue)
            {
                var customer = await _context.Customer
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value);

                sale.Customer = customer;
            }
        }

        /// <summary>
        /// Resolves a file path under wwwroot so the logo (and other assets) are found
        /// whether the app runs from project dir, bin, or published output.
        /// </summary>
        private static string? ResolveWwwRootPath(params string[] relativePathParts)
        {
            var relativePath = Path.Combine(relativePathParts);
            var candidates = new List<string>
            {
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "wwwroot", relativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "wwwroot", relativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot", relativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot", relativePath)
            };
            foreach (var path in candidates)
            {
                try
                {
                    var full = Path.GetFullPath(path);
                    if (File.Exists(full))
                        return full;
                }
                catch { /* skip invalid paths */ }
            }
            return null;
        }
    }
}
