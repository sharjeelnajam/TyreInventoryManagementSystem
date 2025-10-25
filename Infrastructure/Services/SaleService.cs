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
                var existingSales = await _context.Sale.Include(p => p.SaleDetails).FirstOrDefaultAsync(p => p.Id == sale.Id);


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

                // --- Sync PurchaseDetails ---
                var updatedDetailIds = sale.SaleDetails?.Select(s => s.Id).ToList() ?? new List<Guid>();

                var toRemove = _context.SaleDetail.Where(s => !updatedDetailIds.Contains(s.Id) && s.SaleId == sale.Id).ToList();
                var availAbleStocks = _context.StockHistories.ToList();

                foreach (var r in toRemove)
                {
                    var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == r.ProductId);

                    if (stockProduct != null)
                    {
                        stockProduct.NewStockLevel = stockProduct.NewStockLevel + r.Quantity;
                        _context.StockHistories.Update(stockProduct);
                    }

                    _context.SaleDetail.Remove(r);
                    await _context.SaveChangesAsync();
                }

                // Add or Update
                if (sale.SaleDetails != null && sale.SaleDetails.Any())
                {
                    foreach (var detail in sale.SaleDetails)
                    {
                        //stock product state 5
                        var stockProduct = availAbleStocks.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        var existingDetail = existingSales.SaleDetails.FirstOrDefault(s => s.Id == detail.Id);


                        if (existingDetail != null && stockProduct != null)
                        {
                            //previous value was 5
                            //3<5
                            if (detail.Quantity < existingDetail.Quantity)
                            {
                                int newValue = existingDetail.Quantity - detail.Quantity;


                                // Update existing
                                existingDetail.ProductId = detail.ProductId;
                                existingDetail.Quantity = detail.Quantity;
                                existingDetail.UnitPrice = detail.UnitPrice;
                                existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;

                                stockProduct.NewStockLevel = stockProduct.NewStockLevel + newValue;
                                stockProduct.UpdatedAt = DateTime.Now;
                                _context.StockHistories.Update(stockProduct);

                            }
                            else if (detail.Quantity > existingDetail.Quantity)
                            {
                                var newValue = detail.Quantity - existingDetail.Quantity;

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
                                // Quantity same — update price and total only
                                existingDetail.UnitPrice = detail.UnitPrice;
                                existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;
                            }
                                _context.SaleDetail.Update(existingDetail);

                        }
                        else
                        {
                            // Add new
                            if (detail.Id == Guid.Empty)
                                detail.Id = Guid.NewGuid();

                            detail.SaleId = existingSales.Id;
                            detail.TotalPrice = detail.Quantity * detail.UnitPrice;

                            stockProduct.NewStockLevel = stockProduct.NewStockLevel - detail.Quantity;
                            _context.StockHistories.Update(stockProduct);
                            stockProduct.UpdatedAt = DateTime.Now;

                           _context.SaleDetail.Add(detail);
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
