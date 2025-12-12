using Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Identity.Client;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Infrastructure.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ITenantProvider _tenantProvider;

        public PurchaseService(ApplicationDbContext context, AuthenticationStateProvider authenticationStateProvider, ITenantProvider tenantProvider)
        {
            _context = context;
            _authStateProvider = authenticationStateProvider;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<Purchase>> GetAllPurchasesAsync()
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    return await _context.Purchase
                         .Where(p => p.TenantId == _tenantProvider.TenantId
                                     && p.SupplierId != null)   // <<< filter added
                         .Include(p => p.Supplier)
                         .Include(p => p.PurchaseDetails)
                             .ThenInclude(d => d.Product)
                         .ToListAsync();
                }

                else return new List<Purchase>();
                
            }
            catch (Exception)
            {

                throw;
            }
         
        }

        public async Task<Purchase> GetPurchaseByIdAsync(Guid id)
        {
            try
            {
                if (_tenantProvider.TenantId == Guid.Empty)
                {
                    var purchase = await _context.Purchase
                          .Where(p => p.TenantId == _tenantProvider.TenantId)
                          .Include(p => p.Supplier)
                          .Include(p => p.PurchaseDetails)
                          .ThenInclude(d => d.Product)
                          .FirstOrDefaultAsync(p => p.Id == id);

                        return purchase;
                }
                else
                    return new Purchase();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task AddPurchaseAsync(Purchase purchase)
        {
            // Create a transaction to ensure all operations succeed or fail together
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                purchase.Id = Guid.NewGuid();
                purchase.CreatedAt = DateTime.Now;
                var purchaseDetails = new List<PurchaseDetail>();

                _context.Purchase.Add(purchase);
                if (purchaseDetails.Any())
                    await _context.PurchaseDetails.AddRangeAsync(purchaseDetails);

                await _context.SaveChangesAsync();

                // Update stock, cost price, and history
                if (purchase.PurchaseDetails != null && purchase.PurchaseDetails.Any())
                {
                    foreach (var detail in purchase.PurchaseDetails)
                    {
                        // Get current logged-in user
                        var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
                        var currentUser = authState.User;
                        var performedBy = currentUser.FindFirst(ClaimTypes.Name)?.Value
                            ?? currentUser.FindFirst("name")?.Value
                            ?? currentUser.Identity?.Name;

                        // Fetch product
                        var product = await _context.Products.FindAsync(detail.ProductId);
                        if (product == null)
                            throw new Exception($"Product with ID {detail.ProductId} not found.");

                        // Capture old stock (before update)
                        var oldStock = detail.Quantity;

                        // Update stock quantity
                        //detail.Quantity += detail.Quantity;

                        // Recalculate average cost price using weighted average formula
                        var oldCost = product.AverageCostPrice;
                        var newQty = detail.Quantity;
                        var newCost = detail.UnitPrice;

                        var totalCost = (oldCost * oldStock) + (newCost * newQty);
                        var totalQty = oldStock + newQty;
                        product.AverageCostPrice = totalQty == 0 ? 0 : Math.Round(totalCost / totalQty, 2);

                        _context.Update(product);

                        // Check for existing stock history entry for product
                        var stockProduct = await _context.StockHistories
                            .FirstOrDefaultAsync(p => p.ProductId == detail.ProductId);

                        if (stockProduct != null)
                        {
                            // Update existing record if found
                            stockProduct.NewStockLevel = stockProduct.NewStockLevel + detail.Quantity;
                            stockProduct.QuantityChanged = detail.Quantity;
                            stockProduct.ActionDate = DateTime.UtcNow;
                            stockProduct.PerformedBy = performedBy;
                            _context.Update(stockProduct);
                        }
                        else
                        {
                            // Add new stock history entry
                            var history = new StockHistory
                            {
                                ProductId = product.Id,
                                ActionType = "Purchase",
                                QuantityChanged = detail.Quantity,
                                PreviousStockLevel = oldStock,
                                NewStockLevel = detail.Quantity,
                                ReferenceNumber = purchase.PurchaseNumber,
                                ReferenceId = purchase.Id,
                                PerformedBy = performedBy,
                                ActionDate = purchase.PurchaseDate
                            };
                            _context.Add(history);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Commit only if everything succeeds
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(); // Rollback if any operation fails
                throw;
            }
        }

        public async Task UpdatePurchaseAsync(Purchase purchase)
        {
            // 🔹 NEW CODE START: added transaction handling
            using var tx = await _context.Database.BeginTransactionAsync();
            // 🔹 NEW CODE END

            try
            {
                var existing = await _context.Purchase
                    .AsNoTracking()
                    .Include(p => p.PurchaseDetails)
                    .FirstOrDefaultAsync(p => p.Id == purchase.Id);

                if (existing == null)
                    throw new Exception("Purchase not found in DB");

                // --- Update only safe fields ---
                existing.PurchaseNumber = purchase.PurchaseNumber;
                existing.PurchaseDate = purchase.PurchaseDate;
                existing.SupplierId = purchase.SupplierId;
                existing.Discount = purchase.Discount;
                existing.TaxAmount = purchase.TaxAmount;
                existing.TotalAmount = purchase.TotalAmount;
                existing.NetAmount = purchase.NetAmount;
                existing.PaymentStatus = purchase.PaymentStatus;
                existing.UpdatedAt = DateTime.Now;

                // --- Sync PurchaseDetails ---
                var updatedDetailIds = purchase.PurchaseDetails?.Select(d => d.Id).ToList() ?? new List<Guid>();

                // Delete missing
                var toRemove = _context.PurchaseDetails
                    .Where(d => !updatedDetailIds.Contains(d.Id) && d.PurchaseId == purchase.Id)
                    .ToList();
                var availAbleStocks = _context.StockHistories.ToList();

                foreach (var r in toRemove)
                {
                    var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == r.ProductId);
                    if (stockProduct != null)
                    {
                        stockProduct.NewStockLevel = stockProduct.NewStockLevel - r.Quantity;
                        _context.StockHistories.Update(stockProduct);
                    }
                    _context.PurchaseDetails.Remove(r);
                    await _context.SaveChangesAsync();
                }

                // Add or Update
                if (purchase.PurchaseDetails != null && purchase.PurchaseDetails.Any())
                {
                    foreach (var detail in purchase.PurchaseDetails)
                    {
                        var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        var existingDetail = existing.PurchaseDetails.FirstOrDefault(d => d.Id == detail.Id);

                        if (existingDetail != null && stockProduct != null)
                        {
                            if (detail.Quantity < existingDetail.Quantity)
                            {
                                int newValue = existingDetail.Quantity - detail.Quantity;

                                if (newValue <= stockProduct.NewStockLevel)
                                {
                                    existingDetail.ProductId = detail.ProductId;
                                    existingDetail.Quantity = detail.Quantity;
                                    existingDetail.UnitPrice = detail.UnitPrice;
                                    existingDetail.SellingPrice = detail.SellingPrice;
                                    existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;

                                    stockProduct.NewStockLevel = stockProduct.NewStockLevel - newValue;
                                    stockProduct.UpdatedAt = DateTime.Now;
                                    _context.StockHistories.Update(stockProduct);
                                }
                                else
                                {
                                    throw new Exception($"you can not decrease the quantity because available stock is {stockProduct.NewStockLevel}");
                                }
                            }
                            else if (detail.Quantity > existingDetail.Quantity)
                            {
                                var newValue = detail.Quantity - existingDetail.Quantity;

                                existingDetail.ProductId = detail.ProductId;
                                existingDetail.Quantity = detail.Quantity;
                                existingDetail.UnitPrice = detail.UnitPrice;
                                existingDetail.SellingPrice = detail.SellingPrice;
                                existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;

                                stockProduct.NewStockLevel = stockProduct.NewStockLevel + newValue;
                                stockProduct.UpdatedAt = DateTime.Now;
                                _context.StockHistories.Update(stockProduct);
                            }
                        }
                        else
                        {
                            if (detail.Id == Guid.Empty)
                                detail.Id = Guid.NewGuid();

                            detail.PurchaseId = existing.Id;
                            detail.TotalPrice = detail.Quantity * detail.UnitPrice;

                            _context.PurchaseDetails.Add(detail);

                            if (stockProduct == null)
                            {
                                var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
                                var currentUser = authState.User;
                                var performedBy = currentUser.FindFirst(ClaimTypes.Name)?.Value ?? currentUser.FindFirst("name")?.Value ?? currentUser.Identity?.Name;

                                var newStock = new StockHistory
                                {
                                    NewStockLevel = detail.Quantity,
                                    CreatedAt = DateTime.Now,
                                    ActionDate = DateTime.Now,
                                    ActionType = "Purchase",
                                    QuantityChanged = detail.Quantity,
                                    ProductId = detail.ProductId,
                                    ReferenceId = purchase.Id,
                                    ReferenceNumber = purchase.PurchaseNumber,
                                    PerformedBy = performedBy,
                                };
                                _context.StockHistories.Add(newStock);
                            }
                            else
                            {
                                stockProduct.NewStockLevel = stockProduct.NewStockLevel + detail.Quantity;
                                _context.StockHistories.Update(stockProduct);
                                stockProduct.UpdatedAt = DateTime.Now;
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // 🔹 NEW CODE START: Recalculate product AverageCostPrice and Quantity
                var productIds = purchase.PurchaseDetails.Select(d => d.ProductId).Distinct().ToList();

                foreach (var pid in productIds)
                {
                    var product = await _context.Products.Include(p => p.PurchaseDetails)
                        .FirstOrDefaultAsync(p => p.Id == pid);

                    if (product != null)
                    {
                        var allDetails = await _context.PurchaseDetails
                            .Where(pd => pd.ProductId == pid)
                            .ToListAsync();

                        var totalQty = allDetails.Sum(x => x.Quantity);
                        var totalCost = allDetails.Sum(x => x.UnitPrice * x.Quantity);
                        product.AverageCostPrice = totalQty == 0 ? 0 : Math.Round(totalCost / totalQty, 2);

                        var purchaseQty = await _context.PurchaseDetails.Where(pd => pd.ProductId == pid).SumAsync(pd => pd.Quantity);
                        var saleQty = await _context.SaleDetail.Where(sd => sd.ProductId == pid).SumAsync(sd => sd.Quantity);
                        //product.Quantity = purchaseQty - saleQty;
                    }
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                // 🔹 NEW CODE END
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine("⚠️ Concurrency error: " + ex.Message);
                await tx.RollbackAsync(); // 🔹 NEW LINE
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Update error: " + ex.Message);
                await tx.RollbackAsync(); // 🔹 NEW LINE
                throw;
            }
        }

        public async Task DeletePurchaseAsync(Guid id)
        {
            var purchase = await _context.Purchase.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase != null)
            {
                _context.Purchase.Remove(purchase);  // triggers soft delete logic
                await _context.SaveChangesAsync();   // ChangeTracker handles IsDeleted/DeletedAt
            }
        }

        public async Task<List<PurchaseDetail>> GetPurchaseDetailsByProductIdAsync(Guid productId)
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {
                    return await _context.PurchaseDetails
                          .Include(pd => pd.Purchase)
                          .Where(pd => pd.ProductId == productId && pd.TenantId == _tenantProvider.TenantId)
                          .OrderByDescending(pd => pd.Purchase.PurchaseDate)
                          .ToListAsync();
                }
                return new List<PurchaseDetail>();
            }
            catch (Exception)
            {

                throw;
            }

        }
    }
}
