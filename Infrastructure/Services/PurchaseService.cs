using Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthenticationStateProvider _authStateProvider;

        public PurchaseService(ApplicationDbContext context, AuthenticationStateProvider authenticationStateProvider)
        {
            _context = context;
            _authStateProvider = authenticationStateProvider;
        }

        public async Task<List<Purchase>> GetAllPurchasesAsync()
        {
            return await _context.Purchase
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
                .ToListAsync();
        }

        public async Task<Purchase> GetPurchaseByIdAsync(Guid id)
        {
            var purchase = await _context.Purchase
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase != null)
                return purchase;
            return new Purchase();
        }

        public async Task AddPurchaseAsync(Purchase purchase)
        {
            // Create a transaction to ensure all operations succeed or fail together
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                purchase.Id = Guid.NewGuid();
                purchase.CreatedAt = DateTime.UtcNow;
                var purchaseDetails = new List<PurchaseDetail>();

                //if (purchase.PurchaseDetails != null)
                //{
                //    foreach (var detail in purchase.PurchaseDetails)
                //    {
                //        detail.PurchaseId = purchase.Id;
                //        detail.TotalPrice = detail.Quantity * detail.UnitPrice;
                //        purchaseDetails.Add(detail);
                //    }
                //}

                _context.Purchase.Add(purchase);
                if (purchaseDetails.Any())
                    await _context.PurchaseDetails.AddRangeAsync(purchaseDetails);

                await _context.SaveChangesAsync();

                // Update stock and history
                if (purchase.PurchaseDetails != null && purchase.PurchaseDetails.Any())
                {

                    foreach (var detail in purchase.PurchaseDetails)
                    {
                        var authState = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
                        var currentUser = authState.User;
                        var performedBy = currentUser.FindFirst(ClaimTypes.Name)?.Value ?? currentUser.FindFirst("name")?.Value ?? currentUser.Identity?.Name;

                        var stockProduct = _context.StockHistories.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        var product = await _context.Products.FindAsync(detail.ProductId);

                        if (stockProduct != null)
                        {
                            stockProduct.NewStockLevel = stockProduct.NewStockLevel + detail.Quantity;
                            stockProduct.QuantityChanged = detail.Quantity;
                            stockProduct.ActionDate = DateTime.UtcNow;
                            stockProduct.PerformedBy = performedBy;
                            _context.Update(stockProduct);

                        }
                        else
                        {
                            var history = new StockHistory
                            {
                                ProductId = product.Id,
                                ActionType = "Purchase",
                                QuantityChanged = detail.Quantity,
                                NewStockLevel = detail.Quantity,
                                ReferenceNumber = purchase.PurchaseNumber,
                                ReferenceId = purchase.Id,
                                PerformedBy = performedBy, // TODO: Replace with actual user
                                ActionDate = DateTime.UtcNow
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
            try
            {
                var existing = await _context.Purchase.AsNoTracking().Include(p => p.PurchaseDetails).FirstOrDefaultAsync(p => p.Id == purchase.Id);

              
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
                existing.UpdatedAt = DateTime.UtcNow; // best practice

                // --- Sync PurchaseDetails ---
                var updatedDetailIds = purchase.PurchaseDetails?.Select(d => d.Id).ToList() ?? new List<Guid>();

                // Delete missing

                var toRemove = _context.PurchaseDetails.Where(d => !updatedDetailIds.Contains(d.Id) && d.PurchaseId == purchase.Id).ToList();
                var availAbleStocks = _context.StockHistories.ToList();
                foreach (var r in toRemove)
                {
                    var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == r.ProductId);
                    if(stockProduct != null && r.Quantity < stockProduct.NewStockLevel)
                    {
                        _context.PurchaseDetails.Remove(r);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        string error = $"you can not remove this {r.Product.ProductName} product ";
                    }
                }

                // Add or Update
                if (purchase.PurchaseDetails != null && purchase.PurchaseDetails.Any())
                {
                    foreach (var detail in purchase.PurchaseDetails)
                    {
                        //stock product state 5
                        var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        var existingDetail = existing.PurchaseDetails.FirstOrDefault(d => d.Id == detail.Id);

                        
                        if (existingDetail != null && stockProduct != null)
                        {
                            if(detail.Quantity < existingDetail.Quantity)
                            {

                                //int availAbleStockQuantity = existingDetail.Quantity - stockProduct.NewStockLevel;
                                //new value previous - existing  = 8
                                int newValue = existingDetail.Quantity - detail.Quantity;

                                if (newValue <= stockProduct.NewStockLevel)
                                {
                                    // Update existing
                                    existingDetail.ProductId = detail.ProductId;
                                    existingDetail.Quantity = detail.Quantity;
                                    existingDetail.UnitPrice = detail.UnitPrice;
                                    existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;

                                    stockProduct.NewStockLevel = stockProduct.NewStockLevel - newValue;
                                    stockProduct.UpdatedAt = DateTime.Now;
                                    _context.StockHistories.Update(stockProduct);
                                }
                                else
                                {
                                    string error = $"you can not decrease the quantity becasue available stock is";
                                }
                            }
                            else if(detail.Quantity > existingDetail.Quantity)
                            {
                                var newValue = detail.Quantity - existingDetail.Quantity;

                                // Update existing
                                existingDetail.ProductId = detail.ProductId;
                                existingDetail.Quantity = detail.Quantity;
                                existingDetail.UnitPrice = detail.UnitPrice;
                                existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;

                                stockProduct.NewStockLevel = stockProduct.NewStockLevel + newValue;
                                stockProduct.UpdatedAt = DateTime.Now;
                                _context.StockHistories.Update(stockProduct);
                            }

                        }
                        else
                        {
                            // Add new
                            if (detail.Id == Guid.Empty)
                                detail.Id = Guid.NewGuid();

                            detail.PurchaseId = existing.Id;
                            detail.TotalPrice = detail.Quantity * detail.UnitPrice;

                            stockProduct.NewStockLevel = stockProduct.NewStockLevel + detail.Quantity;
                            _context.StockHistories.Update(stockProduct);
                            stockProduct.UpdatedAt = DateTime.Now;

                            existing.PurchaseDetails.Add(detail);
                        }
                    }
                }

                await _context.SaveChangesAsync();
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

        public async Task DeletePurchaseAsync(Guid id)
        {
            var purchase = await _context.Purchase.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase != null)
            {
                _context.Purchase.Remove(purchase);  // triggers soft delete logic
                await _context.SaveChangesAsync();   // ChangeTracker handles IsDeleted/DeletedAt
            }
        }
    }
}
