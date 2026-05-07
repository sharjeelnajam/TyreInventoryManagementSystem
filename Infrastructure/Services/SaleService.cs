using Domain;
using Domain.DTO;
using Domain.Enums;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Shared.MultiTenancy;
using System.Data;
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
                var tenantId = _tenantProvider.TenantId;
                var query = _context.Sale
                    .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                    .AsQueryable();
                if (tenantId != Guid.Empty)
                {
                    // Include shop-billing sales that were saved with null TenantId but belong to this branch's bill
                    query = query.Where(s =>
                        s.TenantId == tenantId
                        || (s.TenantId == null && s.ShopServiceBillId != null
                            && _context.ShopServiceBills.Any(b => b.Id == s.ShopServiceBillId && b.TenantId == tenantId)));
                }
                var sales = await query.ToListAsync();
                await HydrateSalesWithCustomers(sales);
                return sales ?? new List<Sale>();
               
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
                var sale = await _context.Sale
                    .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == id);
                if (sale == null) return null;
                var tenantId = _tenantProvider.TenantId;
                if (tenantId != Guid.Empty && sale.TenantId != tenantId)
                {
                    if (sale.TenantId != null || sale.ShopServiceBillId is not Guid billId)
                        return null;
                    var billTenant = await _context.ShopServiceBills.AsNoTracking()
                        .Where(b => b.Id == billId)
                        .Select(b => b.TenantId)
                        .FirstOrDefaultAsync();
                    if (!billTenant.HasValue || billTenant.Value != tenantId)
                        return null;
                }
                await HydrateSaleWithCustomer(sale);
                return sale;
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetByIdAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetNextSaleReferencePreviewAsync()
        {
            var tenantId = _tenantProvider.TenantId;
            var max = await UnifiedSaleReference.GetMaxSequenceAsync(_context, tenantId);
            return UnifiedSaleReference.FormatSequence(max + 1);
        }

        public async Task<bool> AddAsync(Sale sale)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    if (sale == null) return false;

                if (sale.CustomerId == Guid.Empty)
                    sale.CustomerId = null;

                await ApplyVehicleNumberFromCustomerIfEmptyAsync(_context, sale);

                // Validate split payment: Card + Cash must equal NetAmount when using "Card & Cash"
                if (string.Equals(sale.PaymentMethod, "Card & Cash", StringComparison.OrdinalIgnoreCase))
                {
                    var sum = sale.CashAmount + sale.CardAmount;
                    if (Math.Abs(sum - sale.NetAmount) > 0.01m)
                        throw new InvalidOperationException($"Split payment invalid: Card (£{sale.CardAmount:N2}) + Cash (£{sale.CashAmount:N2}) = £{sum:N2} must equal Total £{sale.NetAmount:N2}. Checkout not allowed.");
                }

                if (string.IsNullOrWhiteSpace(sale.PaymentMethod))
                    sale.PaymentMethod = "Cash";
                if (string.IsNullOrWhiteSpace(sale.PaymentStatus))
                    sale.PaymentStatus = "Paid";

                sale.Id = Guid.NewGuid();
                sale.CreatedAt = DateTime.Now;
                if (_tenantProvider.TenantId != Guid.Empty)
                    sale.TenantId = _tenantProvider.TenantId;
                else if (!sale.TenantId.HasValue && sale.ShopServiceBillId is Guid shopBillId)
                {
                    var billTenant = await _context.ShopServiceBills.AsNoTracking()
                        .Where(b => b.Id == shopBillId)
                        .Select(b => b.TenantId)
                        .FirstOrDefaultAsync();
                    if (billTenant.HasValue && billTenant.Value != Guid.Empty)
                        sale.TenantId = billTenant;
                }

                if (!sale.ShopServiceBillId.HasValue)
                    sale.SaleNumber = await UnifiedSaleReference.AllocateNextAsync(_context, _tenantProvider.TenantId);

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

                        int previousLevel;
                        int newStockLevel;
                        if (stockProduct != null)
                        {
                            previousLevel = stockProduct.NewStockLevel;
                            newStockLevel = previousLevel - detail.Quantity;
                            stockProduct.NewStockLevel = newStockLevel;
                            stockProduct.QuantityChanged = -detail.Quantity;
                            stockProduct.ActionDate = DateTime.UtcNow;
                            stockProduct.PerformedBy = performedBy;
                            stockProduct.ReferenceId = sale.Id;
                            stockProduct.ReferenceNumber = sale.SaleNumber;
                            stockProduct.ActionType = "Sale";
                            stockProduct.PreviousStockLevel = previousLevel;
                            _context.Update(stockProduct);

                            var saleStockHistory = new StockHistory
                            {
                                Id = Guid.NewGuid(),
                                ProductId = detail.ProductId,
                                ActionType = "Sale",
                                QuantityChanged = -detail.Quantity,
                                PreviousStockLevel = previousLevel,
                                NewStockLevel = newStockLevel,
                                ActionDate = DateTime.UtcNow,
                                ReferenceId = sale.Id,
                                ReferenceNumber = sale.SaleNumber,
                                PerformedBy = performedBy,
                                TenantId = _tenantProvider.TenantId
                            };
                            await _context.StockHistories.AddAsync(saleStockHistory);
                        }
                        else
                        {
                            previousLevel = 0;
                            newStockLevel = Math.Max(0, previousLevel - detail.Quantity);
                            var newStockHistory = new StockHistory
                            {
                                Id = Guid.NewGuid(),
                                ProductId = detail.ProductId,
                                ActionType = "Sale",
                                QuantityChanged = -detail.Quantity,
                                PreviousStockLevel = previousLevel,
                                NewStockLevel = newStockLevel,
                                ActionDate = DateTime.UtcNow,
                                ReferenceId = sale.Id,
                                ReferenceNumber = sale.SaleNumber,
                                PerformedBy = performedBy,
                                TenantId = _tenantProvider.TenantId
                            };
                            await _context.StockHistories.AddAsync(newStockHistory);
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
            });
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

                await ApplyVehicleNumberFromCustomerIfEmptyAsync(ctx, sale);

                // Update fields
                existingSales.SaleNumber = sale.SaleNumber;
                existingSales.CustomerId = sale.CustomerId;
                existingSales.TotalAmount = sale.TotalAmount;
                existingSales.Discount = sale.Discount;
                existingSales.TaxAmount = sale.TaxAmount;
                existingSales.VatMode = sale.VatMode;
                existingSales.NetAmount = sale.NetAmount;
                existingSales.PaymentMethod = sale.PaymentMethod;
                existingSales.PaymentStatus = sale.PaymentStatus;
                existingSales.DueDate = sale.DueDate;
                existingSales.VehicleNumber = sale.VehicleNumber;
                existingSales.IsApproved = sale.IsApproved;
                existingSales.Notes = sale.Notes;
                existingSales.JobDescription = sale.JobDescription;
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

                            existingDetail.LineDisplayName = detail.LineDisplayName;

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

            await HydrateSaleWithCustomer(sale);

            if (sale == null)
                return Array.Empty<byte>();

            List<(string desc, int qty, decimal unit, decimal total)> lineItems;

            if (sale.ShopServiceBillId.HasValue)
            {
                var bill = await _context.ShopServiceBills
                    .Include(b => b.Items)
                    .FirstOrDefaultAsync(b => b.Id == sale.ShopServiceBillId.Value);

                lineItems = bill?.Items?
                    .Select(i => (i.ServiceName, i.Quantity, i.UnitPrice, i.TotalPrice))
                    .ToList()
                    ?? new List<(string, int, decimal, decimal)>();
            }
            else
            {
                lineItems = (sale.SaleDetails ?? new List<SaleDetail>())
                    .Select(d => (SaleDetailLineDisplay.InvoiceDescription(d), d.Quantity, d.UnitPrice, d.TotalPrice))
                    .ToList();
            }

            var logoPath = ResolveWwwRootPath("uploads", "logo", "logo.png");
            byte[]? logoData = !string.IsNullOrEmpty(logoPath) && File.Exists(logoPath)
                ? await File.ReadAllBytesAsync(logoPath)
                : null;

            QuestPDF.Settings.License = LicenseType.Community;

            var subTotal = sale.TotalAmount;
            var discountAmount = sale.Discount ?? 0m;
            var vatMode = GetVatModeFromSale(sale);
            var vatBreakdown = VatCalculator.Calculate(subTotal, discountAmount, vatMode);
            var vatAmount = vatMode == VatMode.ExcludeVat ? 0m : vatBreakdown.VatAmount;
            var totalDue = vatBreakdown.NetAmount;
            var vatLabel = vatMode switch
            {
                VatMode.IncludingVat => "VAT Included (20%)",
                VatMode.PlusVat => "VAT (20%)",
                _ => "VAT (0%)"
            };
            var vatStatusLabel = vatMode switch
            {
                VatMode.PlusVat => "Plus VAT",
                VatMode.IncludingVat => "Including VAT",
                _ => "Exclude VAT"
            };

            var accent = Color.FromHex("#1e3a5f");
            var muted = Color.FromHex("#64748b");
            var border = Color.FromHex("#e2e8f0");
            var tableHeader = Color.FromHex("#1e3a5f");
            var rowAlt = Color.FromHex("#f1f5f9");

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(40);
                    page.MarginVertical(36);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                    page.Header().Column(col =>
                    {
                        if (logoData != null)
                            col.Item().PaddingTop(8).AlignCenter().Width(200).Image(logoData).FitArea();
                    });

                    page.Content().Column(contentCol =>
                    {
                        contentCol.Item().PaddingTop(logoData != null ? 8 : 20).Row(topRow =>
                        {
                            topRow.RelativeItem().Column(left =>
                            {
                                left.Item().Text("INVOICE").FontSize(22).Bold().FontColor(accent);
                                left.Item().PaddingTop(8).Text(text =>
                                {
                                    text.DefaultTextStyle(x => x.FontSize(10));
                                    text.Span("Invoice number  ").FontColor(muted);
                                    text.Span(sale.SaleNumber ?? "N/A").Bold().FontSize(11);
                                });
                            });
                            topRow.ConstantItem(150).AlignRight().Column(right =>
                            {
                                right.Item().AlignRight().Text("Issue date").FontSize(9).FontColor(muted);
                                right.Item().AlignRight().PaddingTop(2).Text($"{sale.SaleDate:dd MMM yyyy}").FontSize(12).Bold();
                            });
                        });

                        contentCol.Item().PaddingTop(22)
                            .Border(1)
                            .BorderColor(border)
                            .Background(Color.FromHex("#f8fafc"))
                            .Padding(16)
                            .Column(billTo =>
                            {
                                billTo.Item().Text("Bill to").FontSize(9).FontColor(muted).Bold();
                                var billName = sale.Customer?.Name ?? sale.CustomerName ?? "—";
                                billTo.Item().PaddingTop(6).Text(billName).FontSize(12).Bold();
                                var email = sale.Customer?.Email;
                                billTo.Item().PaddingTop(4).Text(text =>
                                {
                                    text.Span("Email  ").FontSize(9).FontColor(muted);
                                    text.Span(string.IsNullOrWhiteSpace(email) ? "—" : email).FontSize(10);
                                });
                            });

                        contentCol.Item().PaddingTop(24).Table(table =>
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
                                header.Cell().Background(tableHeader).PaddingVertical(10).PaddingHorizontal(10)
                                    .Text("Description").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(tableHeader).PaddingVertical(10).PaddingHorizontal(10)
                                    .AlignMiddle().Text("Qty").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(tableHeader).PaddingVertical(10).PaddingHorizontal(10)
                                    .AlignRight().Text("Unit price").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(tableHeader).PaddingVertical(10).PaddingHorizontal(10)
                                    .AlignRight().Text("Total").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            var rowIndex = 0;
                            foreach (var item in lineItems)
                            {
                                var bg = rowIndex % 2 == 0 ? Colors.White : rowAlt;
                                rowIndex++;
                                table.Cell().BorderBottom(1).BorderColor(border).Background(bg).PaddingVertical(10).PaddingHorizontal(10)
                                    .Text(item.desc).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(border).Background(bg).PaddingVertical(10).PaddingHorizontal(10)
                                    .AlignMiddle().Text(item.qty.ToString()).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(border).Background(bg).PaddingVertical(10).PaddingHorizontal(10)
                                    .AlignRight().Text($"£{item.unit:0.00}").FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(border).Background(bg).PaddingVertical(10).PaddingHorizontal(10)
                                    .AlignRight().Text($"£{item.total:0.00}").FontSize(10).Bold();
                            }
                        });

                        contentCol.Item().PaddingTop(24).AlignRight().Width(280).Column(totalsCol =>
                        {
                            totalsCol.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Subtotal").FontSize(10).FontColor(muted);
                                r.ConstantItem(100).AlignRight().Text($"£{subTotal:0.00}").FontSize(10);
                            });
                            totalsCol.Item().PaddingTop(6).Row(r =>
                            {
                                r.RelativeItem().Text("Discount").FontSize(10).FontColor(muted);
                                r.ConstantItem(100).AlignRight().Text($"-£{discountAmount:0.00}").FontSize(10);
                            });
                            if (vatMode != VatMode.ExcludeVat)
                            {
                                totalsCol.Item().PaddingTop(6).Row(r =>
                                {
                                    r.RelativeItem().Text(vatLabel).FontSize(10).FontColor(muted);
                                    r.ConstantItem(100).AlignRight().Text($"£{vatAmount:0.00}").FontSize(10);
                                });
                                totalsCol.Item().PaddingTop(6).Row(r =>
                                {
                                    r.RelativeItem().Text("VAT Status").FontSize(10).FontColor(muted);
                                    r.ConstantItem(100).AlignRight().Text(vatStatusLabel).FontSize(10);
                                });
                            }
                            totalsCol.Item().PaddingTop(12).LineHorizontal(1).LineColor(accent);
                            totalsCol.Item().PaddingTop(10).Row(r =>
                            {
                                r.RelativeItem().Text("Total due").FontSize(12).Bold().FontColor(accent);
                                r.ConstantItem(100).AlignRight().Text($"£{totalDue:0.00}").FontSize(14).Bold().FontColor(accent);
                            });
                        });
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingTop(16).LineHorizontal(1).LineColor(border);
                        col.Item().AlignCenter().PaddingTop(14)
                            .Text("Thank you for your business")
                            .Italic()
                            .FontSize(11)
                            .FontColor(muted);

                        col.Item().PaddingTop(12)
                            .Row(row =>
                            {
                                row.RelativeItem().AlignMiddle().AlignLeft().Column(left =>
                                {
                                    left.Item().Text("15 Davidson Street").FontSize(9).FontColor(muted);
                                    left.Item().Text("G40 4NS Glasgow").FontSize(9).FontColor(muted);
                                });

                                row.ConstantItem(260).AlignMiddle().AlignCenter().Column(center =>
                                {
                                    center.Item().AlignCenter().Text("H&H").Bold().FontSize(11).FontColor(accent);
                                    center.Item().AlignCenter().Text("A company of EcoTrack Holdings").FontSize(8).FontColor(muted);
                                    center.Item().AlignCenter().Text("Ltd, Vat No. 456042895 & Company No. SC789723").FontSize(8).FontColor(muted);
                                });

                                row.RelativeItem().AlignMiddle().AlignRight().Column(right =>
                                {
                                    right.Item().Text("Tel: 0141 554 0516").FontSize(9).FontColor(muted);
                                });
                            });
                    });
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }
        /// <summary>Thermal-style receipt PDF (narrow layout).</summary>
        public async Task<byte[]> GenerateThermalReceiptPdfAsync(Guid saleId)
        {
            var sale = await _context.Sale
                .Include(x => x.SaleDetails)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == saleId);
            await HydrateSaleWithCustomer(sale);

            if (sale == null)
                return Array.Empty<byte>();

            var thermalLines = new List<(string Desc, string? Remarks, int Qty, decimal Unit, decimal Total)>();
            if (sale.ShopServiceBillId.HasValue)
            {
                var billItems = await _context.ShopServiceBillItems
                    .AsNoTracking()
                    .Where(i => i.ShopServiceBillId == sale.ShopServiceBillId.Value)
                    .OrderBy(i => i.CreatedAt)
                    .ToListAsync();
                foreach (var i in billItems)
                {
                    var desc = string.IsNullOrWhiteSpace(i.ServiceName) ? "Service" : i.ServiceName;
                    thermalLines.Add((desc, i.Remarks, i.Quantity, i.UnitPrice, i.TotalPrice));
                }
            }
            else
            {
                foreach (var d in (sale.SaleDetails ?? new List<SaleDetail>()).OrderBy(x => x.CreatedAt))
                    thermalLines.Add((SaleDetailLineDisplay.ReceiptDescription(d), null, d.Quantity, d.UnitPrice, d.TotalPrice));
            }

            var customerDisplay =
                !string.IsNullOrWhiteSpace(sale.Customer?.Name) ? sale.Customer!.Name
                : !string.IsNullOrWhiteSpace(sale.CustomerName) ? sale.CustomerName
                : null;

            var registrationDisplay =
                !string.IsNullOrWhiteSpace(sale.Customer?.VehicleNumber) ? sale.Customer!.VehicleNumber!.Trim()
                : !string.IsNullOrWhiteSpace(sale.VehicleNumber) ? sale.VehicleNumber.Trim()
                : null;

            var emailDisplay =
                !string.IsNullOrWhiteSpace(sale.Customer?.Email) ? sale.Customer!.Email!.Trim()
                : null;

            const string sepShort = "* * * * * * * * * * * * * * * *";
            const string sep = "********************************";
            var receiptDate = sale.SaleDate.ToString("dd MMM yyyy HH:mm");
            var refText = sale.SaleNumber ?? sale.Id.ToString("N")[..8];
            var thermalVatMode = GetVatModeFromSale(sale);
            var thermalBreakdown = VatCalculator.Calculate(sale.TotalAmount, sale.Discount ?? 0m, thermalVatMode);
            var thermalVat = thermalVatMode == VatMode.ExcludeVat ? 0m : thermalBreakdown.VatAmount;
            var thermalTotal = thermalBreakdown.NetAmount;
            var thermalVatLabel = thermalVatMode switch
            {
                VatMode.IncludingVat => "VAT Included (20%)",
                VatMode.PlusVat => "VAT (20%)",
                _ => "VAT (0%)"
            };
            var thermalVatStatusLabel = thermalVatMode switch
            {
                VatMode.PlusVat => "Plus VAT",
                VatMode.IncludingVat => "Including VAT",
                _ => "Exclude VAT"
            };

            QuestPDF.Settings.License = LicenseType.Community;

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);

                    const float receiptBodyWidth = 320f;
                    const float labelWidth = 72f;
                    const float labelWidthLong = 118f;
                    var muted = Colors.Grey.Medium;

                    page.Content().Row(outer =>
                    {
                        outer.RelativeItem();
                        outer.ConstantItem(receiptBodyWidth).Column(body =>
                        {
                            body.Item().AlignCenter().Text(sepShort).FontSize(7);
                            body.Item().PaddingTop(4).AlignCenter().Text(sep).FontSize(7);
                            body.Item().PaddingTop(2).AlignCenter().Text("PAYMENT RECEIPT").Bold().FontSize(11);
                            body.Item().PaddingBottom(2).AlignCenter().Text(sep).FontSize(7);

                            body.Item().Row(r =>
                            {
                                r.ConstantItem(labelWidth).Text("Ref").FontSize(8);
                                r.RelativeItem().AlignRight().Text(refText).FontSize(8);
                            });
                            body.Item().Row(r =>
                            {
                                r.ConstantItem(labelWidth).Text("Date").FontSize(8);
                                r.RelativeItem().AlignRight().Text(receiptDate).FontSize(8);
                            });
                            if (!string.IsNullOrWhiteSpace(customerDisplay))
                            {
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidth).Text("Customer").FontSize(8);
                                    r.RelativeItem().AlignRight().Text(customerDisplay).FontSize(8);
                                });
                            }
                            if (!string.IsNullOrWhiteSpace(registrationDisplay))
                            {
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidthLong).Text("Registration Number").FontSize(8);
                                    r.RelativeItem().AlignRight().Text(registrationDisplay).FontSize(8);
                                });
                            }
                            if (!string.IsNullOrWhiteSpace(emailDisplay))
                            {
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidth).Text("Email").FontSize(8);
                                    r.RelativeItem().AlignRight().Text(emailDisplay).FontSize(8);
                                });
                            }

                            body.Item().PaddingTop(4).AlignCenter().Text(sep).FontSize(7);

                            body.Item().PaddingTop(2).Row(r =>
                            {
                                r.RelativeItem().Text("ITEM").Bold().FontSize(8);
                                r.ConstantItem(52).AlignRight().Text("AMT").Bold().FontSize(8);
                            });

                            if (!thermalLines.Any())
                                body.Item().PaddingTop(6).AlignCenter().Text("No line items on file.").Italic().FontSize(8).FontColor(muted);

                            foreach (var line in thermalLines)
                            {
                                body.Item().PaddingTop(4).Column(block =>
                                {
                                    block.Item().Text(line.Desc).Bold().FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(line.Remarks))
                                        block.Item().Text($" — {line.Remarks}").FontSize(8).FontColor(muted);
                                    block.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text($"x{line.Qty} @ {line.Unit:N2}").FontSize(7).FontColor(muted);
                                        r.ConstantItem(55).AlignRight().Text(line.Total.ToString("N2")).Bold().FontSize(9);
                                    });
                                });
                            }

                            body.Item().PaddingTop(6).AlignCenter().Text(sep).FontSize(7);

                            body.Item().Row(r =>
                            {
                                r.ConstantItem(labelWidth).Text("Subtotal").FontSize(8);
                                r.RelativeItem().AlignRight().Text(sale.TotalAmount.ToString("N2")).FontSize(8);
                            });
                            if (sale.Discount is decimal disc && disc > 0)
                            {
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidth).Text("Discount").FontSize(8);
                                    r.RelativeItem().AlignRight().Text($"-{disc:N2}").FontSize(8);
                                });
                            }
                            if (thermalVatMode != VatMode.ExcludeVat)
                            {
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidth).Text(thermalVatLabel).FontSize(8);
                                    r.RelativeItem().AlignRight().Text(thermalVat.ToString("N2")).FontSize(8);
                                });
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidth).Text("VAT Status").FontSize(8);
                                    r.RelativeItem().AlignRight().Text(thermalVatStatusLabel).FontSize(8);
                                });
                            }
                            body.Item().PaddingTop(2).Row(r =>
                            {
                                r.ConstantItem(labelWidth).Text("TOTAL").Bold().FontSize(10);
                                r.RelativeItem().AlignRight().Text(thermalTotal.ToString("N2")).Bold().FontSize(10);
                            });

                            if (IsSplitPaymentThermal(sale.PaymentMethod))
                            {
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidth).Text("Cash").FontSize(7).FontColor(muted);
                                    r.RelativeItem().AlignRight().Text(sale.CashAmount.ToString("N2")).FontSize(7).FontColor(muted);
                                });
                                body.Item().Row(r =>
                                {
                                    r.ConstantItem(labelWidth).Text("Card").FontSize(7).FontColor(muted);
                                    r.RelativeItem().AlignRight().Text(sale.CardAmount.ToString("N2")).FontSize(7).FontColor(muted);
                                });
                            }

                            body.Item().Row(r =>
                            {
                                r.ConstantItem(labelWidth).Text("Payment").FontSize(8);
                                r.RelativeItem().AlignRight().Text(string.IsNullOrWhiteSpace(sale.PaymentMethod) ? "N/A" : sale.PaymentMethod).FontSize(8);
                            });
                            body.Item().Row(r =>
                            {
                                r.ConstantItem(labelWidth).Text("Status").FontSize(7).FontColor(muted);
                                r.RelativeItem().AlignRight().Text(sale.PaymentStatus ?? "").FontSize(7).FontColor(muted);
                            });

                            body.Item().PaddingTop(8).AlignCenter().Text(sep).FontSize(7);
                            body.Item().PaddingTop(6).AlignCenter().Text("THANK YOU").Bold().FontSize(12);
                            body.Item().PaddingTop(4).AlignCenter().Text(sepShort).FontSize(7);
                        });
                        outer.RelativeItem();
                    });
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }

        private static bool IsSplitPaymentThermal(string? paymentMethod) =>
            !string.IsNullOrWhiteSpace(paymentMethod) &&
            paymentMethod.Contains("card", StringComparison.OrdinalIgnoreCase) &&
            paymentMethod.Contains("cash", StringComparison.OrdinalIgnoreCase);

        private static VatMode GetVatModeFromSale(Sale sale)
        {
            var value = (int)sale.VatMode;
            if (!Enum.IsDefined(typeof(VatMode), value))
                return VatMode.ExcludeVat;

            return sale.VatMode;
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

        public async Task<int> GetInvoiceCountByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                if (_tenantProvider.TenantId == Guid.Empty)
                    return 0;

                var start = startDate.Date;
                var end = endDate.Date.AddDays(1); // inclusive end date

                return await _context.Sale
                    .Where(s =>
                        s.TenantId == _tenantProvider.TenantId &&
                        s.SaleDate >= start &&
                        s.SaleDate < end)
                    .CountAsync();
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

        /// <summary>
        /// Checkout often only sets CustomerId; registration is stored on the customer record (VehicleNumber).
        /// Copy it onto the sale when empty so lists and PDFs show the plate.
        /// </summary>
        private async Task ApplyVehicleNumberFromCustomerIfEmptyAsync(ApplicationDbContext ctx, Sale sale)
        {
            if (!sale.CustomerId.HasValue || !string.IsNullOrWhiteSpace(sale.VehicleNumber))
                return;

            var tenantId = _tenantProvider.TenantId;
            var customer = await ctx.Customer
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value
                    && (tenantId == Guid.Empty || c.TenantId == tenantId));

            if (customer != null && !string.IsNullOrWhiteSpace(customer.VehicleNumber))
                sale.VehicleNumber = customer.VehicleNumber;
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
