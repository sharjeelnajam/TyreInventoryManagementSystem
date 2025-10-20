using Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class SaleService : ISaleService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthenticationStateProvider _authStateProvider;

        public SaleService(ApplicationDbContext context, AuthenticationStateProvider authenticationStateProvider)
        {
            _context = context;
            _authStateProvider = authenticationStateProvider;
        }

        public async Task<List<Sale>> GetAllAsync()
        {
            try
            {
                var Sales = await _context.Sale.Include(s => s.Customer)
                    .Include(s => s.SaleDetails).ThenInclude(s => s.Product) // if relation exists
                    .ToListAsync();

                return Sales ?? new List<Sale>();
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
                return await _context.Sale.Include(s => s.SaleDetails).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetByIdAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AddAsync(Sale sale)
        {
            // Create transaction to ensure all operations succeed or fail together
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (sale == null) return false;

                sale.Id = Guid.NewGuid();
                sale.CreatedAt = DateTime.Now;

                List<SaleDetail> saleDetails = new List<SaleDetail>();

                if (sale.SaleDetails != null)
                {
                    foreach (var detail in sale.SaleDetails)
                    {
                        detail.SaleId = sale.Id;
                        detail.TotalPrice = detail.Quantity * detail.UnitPrice;
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
                    var performedBy = currentUser.FindFirst(ClaimTypes.Name)?.Value ?? currentUser.FindFirst("name")?.Value ?? currentUser.Identity?.Name;

                    foreach (var detail in sale.SaleDetails)
                    {
                        var stockProduct = _context.StockHistories.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        var product = await _context.Products.FindAsync(detail.ProductId);
                        if (stockProduct != null)
                        {
                            stockProduct.NewStockLevel = stockProduct.NewStockLevel - detail.Quantity;
                            stockProduct.QuantityChanged = detail.Quantity;
                            stockProduct.ActionDate = DateTime.UtcNow;
                            _context.Update(stockProduct);
                            // Stock validation
                            //if (product.Quantity < detail.Quantity)
                            //{
                            //    throw new InvalidOperationException($"Insufficient stock for product {product.ProductName}");
                            //}

                            //var previousStock = product.Quantity;
                            //product.Quantity -= detail.Quantity;

                            //var history = new StockHistory
                            //{
                            //    ProductId = product.Id,
                            //    ActionType = "Sale",
                            //    PreviousStockLevel = previousStock,
                            //    QuantityChanged = -detail.Quantity,
                            //    NewStockLevel = product.Quantity,
                            //    ReferenceNumber = sale.SaleNumber,
                            //    ReferenceId = sale.Id,
                            //    PerformedBy = performedBy,
                            //    ActionDate = DateTime.UtcNow
                            //};

                            //_context.StockHistories.Add(history);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Commit only if everything succeeds
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); // Rollback if any operation fails
                Console.WriteLine($"❌ Error in AddAsync: {ex.Message}");
                return false;
            }
        }

        public async Task UpdateAsync(Sale sale)
        {
            try
            {
                // Load existing Sale and related details without tracking
                var existing = await _context.Sale
                    .AsNoTracking()
                    .Include(s => s.SaleDetails)
                    .FirstOrDefaultAsync(s => s.Id == sale.Id);

                if (existing == null)
                    throw new Exception("Sale not found in DB");

                // --- Update safe fields ---
                existing.SaleNumber = sale.SaleNumber;
                existing.CustomerId = sale.CustomerId;
                existing.TotalAmount = sale.TotalAmount;
                existing.Discount = sale.Discount;
                existing.TaxAmount = sale.TaxAmount;
                existing.NetAmount = sale.NetAmount;
                existing.PaymentMethod = sale.PaymentMethod;
                existing.PaymentStatus = sale.PaymentStatus;
                existing.DueDate = sale.DueDate;
                existing.VehicleNumber = sale.VehicleNumber;
                existing.IsApproved = sale.IsApproved;
                existing.UpdatedAt = DateTime.UtcNow;

                // --- Sync SaleDetails ---
                var updatedDetailIds = sale.SaleDetails?.Select(d => d.Id).ToList() ?? new List<Guid>();

                // Find SaleDetails to remove (not present in updated list)
                var toRemove = _context.SaleDetail
                    .Where(d => !updatedDetailIds.Contains(d.Id) && d.SaleId == sale.Id)
                    .ToList();

                var availableStocks = _context.StockHistories.ToList();

                // --- Delete missing SaleDetails safely ---
                foreach (var r in toRemove)
                {
                    var stockProduct = availableStocks.FirstOrDefault(p => p.ProductId == r.ProductId);
                    if (stockProduct != null)
                    {
                        // Return stock back when deleting sale detail
                        stockProduct.NewStockLevel += r.Quantity;
                        stockProduct.UpdatedAt = DateTime.Now;
                        _context.StockHistories.Update(stockProduct);

                        _context.SaleDetail.Remove(r);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        string error = $"⚠️ Cannot remove {r.Product.ProductName} — product stock not found.";
                        Console.WriteLine(error);
                    }
                }

                // --- Add or Update SaleDetails ---
                if (sale.SaleDetails != null && sale.SaleDetails.Any())
                {
                    foreach (var detail in sale.SaleDetails)
                    {
                        var stockProduct = availableStocks.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        var existingDetail = existing.SaleDetails.FirstOrDefault(d => d.Id == detail.Id);

                        if (stockProduct == null)
                        {
                            Console.WriteLine($"⚠️ Stock record not found for product ID {detail.ProductId}");
                            continue;
                        }

                        if (existingDetail != null)
                        {
                            // When updating quantity
                            if (detail.Quantity != existingDetail.Quantity)
                            {
                                int quantityDifference = detail.Quantity - existingDetail.Quantity;

                                if (quantityDifference > 0)
                                {
                                    // Selling more items → reduce stock
                                    if (quantityDifference <= stockProduct.NewStockLevel)
                                    {
                                        stockProduct.NewStockLevel -= quantityDifference;
                                        stockProduct.UpdatedAt = DateTime.Now;
                                        _context.StockHistories.Update(stockProduct);
                                    }
                                    else
                                    {
                                        string error = $"❌ Not enough stock to sell {detail.Product.ProductName}.";
                                        Console.WriteLine(error);
                                        continue;
                                    }
                                }
                                else
                                {
                                    // Selling less items → add back stock
                                    stockProduct.NewStockLevel += Math.Abs(quantityDifference);
                                    stockProduct.UpdatedAt = DateTime.Now;
                                    _context.StockHistories.Update(stockProduct);
                                }
                            }

                            // Update detail record
                            existingDetail.ProductId = detail.ProductId;
                            existingDetail.Quantity = detail.Quantity;
                            existingDetail.UnitPrice = detail.UnitPrice;
                            existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;
                            existingDetail.UpdatedAt = DateTime.Now;

                            _context.SaleDetail.Update(existingDetail);
                        }
                        else
                        {
                            // --- Add new SaleDetail ---
                            if (detail.Id == Guid.Empty)
                                detail.Id = Guid.NewGuid();

                            detail.SaleId = existing.Id;
                            detail.TotalPrice = detail.Quantity * detail.UnitPrice;
                            detail.CreatedAt = DateTime.UtcNow;

                            // Reduce stock for new sale
                            if (detail.Quantity <= stockProduct.NewStockLevel)
                            {
                                stockProduct.NewStockLevel -= detail.Quantity;
                                stockProduct.UpdatedAt = DateTime.Now;
                                _context.StockHistories.Update(stockProduct);
                                _context.SaleDetail.Add(detail);
                            }
                            else
                            {
                                string error = $"❌ Not enough stock available for {detail.Product.ProductName}.";
                                Console.WriteLine(error);
                            }
                        }
                    }
                }

                _context.Sale.Update(existing);
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


        //public async Task UpdateAsync(Sale sale)
        //{

        //    try
        //    {
        //        var existingSale = await _context.Sale.FirstOrDefaultAsync(s => s.Id == sale.Id);
        //        if (existingSale == null) throw new Exception("Purchase not found in DB");

        //        var newSaleDetails = new List<SaleDetail>();

        //        // Update fields
        //        existingSale.SaleNumber = sale.SaleNumber;
        //        existingSale.CustomerId = sale.CustomerId;
        //        existingSale.TotalAmount = sale.TotalAmount;
        //        existingSale.Discount = sale.Discount;
        //        existingSale.TaxAmount = sale.TaxAmount;
        //        existingSale.NetAmount = sale.NetAmount;
        //        existingSale.PaymentMethod = sale.PaymentMethod;
        //        existingSale.PaymentStatus = sale.PaymentStatus;
        //        existingSale.DueDate = sale.DueDate;
        //        existingSale.VehicleNumber = sale.VehicleNumber;
        //        existingSale.IsApproved = sale.IsApproved;
        //        existingSale.UpdatedAt = DateTime.Now;

        //        _context.SaveChanges();

        //        var existingSaleDetails = _context.SaleDetail.Where(s => s.SaleId == sale.Id).ToList();

        //        foreach (var detail in sale.SaleDetails)
        //        {
        //            var existingDetail = existingSaleDetails.FirstOrDefault(d => d.Id == detail.Id);

        //            if (existingDetail != null)
        //            {
        //                detail.CreatedAt = existingDetail.CreatedAt;
        //                detail.UpdatedAt = DateTime.Now;
        //                detail.SaleId = existingSale.Id;
        //                detail.TotalPrice = detail.Quantity * detail.UnitPrice;
        //                detail.Id = Guid.Empty;
        //                newSaleDetails.Add(detail);
        //            }
        //            else
        //            {
        //                detail.CreatedAt = DateTime.Now;
        //                detail.SaleId = existingSale.Id;
        //                detail.TotalPrice = detail.UnitPrice * detail.Quantity;

        //                newSaleDetails.Add(detail);
        //            }
        //        }

        //        _context.RemoveRange(existingSaleDetails);
        //        _context.SaveChanges();
        //        _context.AddRange(newSaleDetails);

        //        _context.SaveChanges();


        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("⚠️ Concurrency error: " + ex.Message);
        //        throw;

        //    }
        //}

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
    }
}
