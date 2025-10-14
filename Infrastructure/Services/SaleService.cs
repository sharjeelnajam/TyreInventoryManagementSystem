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
                var Sales = await _context.Sale.Where(s => s.IsDeleted == false).Include(s => s.Customer)
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
                        var product = await _context.Products.FindAsync(detail.ProductId);
                        if (product != null)
                        {
                            // Stock validation
                            if (product.Quantity < detail.Quantity)
                            {
                                throw new InvalidOperationException($"Insufficient stock for product {product.ProductName}");
                            }

                            var previousStock = product.Quantity;
                            product.Quantity -= detail.Quantity;

                            var history = new StockHistory
                            {
                                ProductId = product.Id,
                                ActionType = "Sale",
                                PreviousStockLevel = previousStock,
                                QuantityChanged = -detail.Quantity,
                                NewStockLevel = product.Quantity,
                                ReferenceNumber = sale.SaleNumber,
                                ReferenceId = sale.Id,
                                PerformedBy = performedBy,
                                ActionDate = DateTime.UtcNow
                            };

                            _context.StockHistories.Add(history);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

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
                var existingSale = await _context.Sale.FirstOrDefaultAsync(s => s.Id == sale.Id);
                if (existingSale == null) throw new Exception("Purchase not found in DB");

                var newSaleDetails = new List<SaleDetail>();

                // Update fields
                existingSale.SaleNumber = sale.SaleNumber;
                existingSale.CustomerId = sale.CustomerId;
                existingSale.TotalAmount = sale.TotalAmount;
                existingSale.Discount = sale.Discount;
                existingSale.TaxAmount = sale.TaxAmount;
                existingSale.NetAmount = sale.NetAmount;
                existingSale.PaymentMethod = sale.PaymentMethod;
                existingSale.PaymentStatus = sale.PaymentStatus;
                existingSale.DueDate = sale.DueDate;
                existingSale.VehicleNumber = sale.VehicleNumber;
                existingSale.IsApproved = sale.IsApproved;
                existingSale.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                var existingSaleDetails = _context.SaleDetail.Where(s => s.SaleId == sale.Id).ToList();

                foreach (var detail in sale.SaleDetails)
                {
                    var existingDetail = existingSaleDetails.FirstOrDefault(d => d.Id == detail.Id);

                    if (existingDetail != null)
                    {
                        detail.CreatedAt = existingDetail.CreatedAt;
                        detail.UpdatedAt = DateTime.Now;
                        detail.SaleId = existingSale.Id;
                        detail.TotalPrice = detail.Quantity * detail.UnitPrice;
                        detail.Id = Guid.Empty;
                        newSaleDetails.Add(detail);
                    }
                    else
                    {
                        detail.CreatedAt = existingDetail.CreatedAt; 
                        detail.SaleId = existingSale.Id;


                        newSaleDetails.Add(detail);

                    }
                }

                _context.RemoveRange(existingSaleDetails);
                _context.SaveChanges();
                _context.AddRange(newSaleDetails);
                _context.SaveChanges();
                //var saleDetailsIds = sale.SaleDetails?.Select(s => s.Id).ToList() ?? new List<Guid>();

                //var toRemove = existingSale.SaleDetails.Where(s => !saleDetailsIds.Contains(s.Id)).ToList();

                //foreach (var s in toRemove)
                //{
                //    _context.SaleDetail.Attach(s);
                //    existingSale.SaleDetails.Remove(s);
                //}
                //if (sale.SaleDetails != null)
                //{
                //    foreach(var saleDetails in sale.SaleDetails)
                //    {
                //        var existingSaleDetails = existingSale.SaleDetails.FirstOrDefault(s => s.Id == saleDetails.Id);

                //        if(existingSaleDetails != null)
                //        {
                //            existingSaleDetails.ProductId = saleDetails.ProductId;
                //            existingSaleDetails.Quantity = saleDetails.Quantity;
                //            existingSaleDetails.UnitPrice = saleDetails.UnitPrice;
                //            existingSaleDetails.TotalPrice = saleDetails.TotalPrice;
                //            existingSaleDetails.UpdatedAt = DateTime.Now;
                //        }
                //        else
                //        {
                //            if (saleDetails.Id == Guid.Empty)
                //                saleDetails.Id = Guid.NewGuid();

                //            saleDetails.SaleId = existingSale.Id;
                //            saleDetails.TotalPrice = saleDetails.Quantity * saleDetails.UnitPrice;

                //            existingSale.SaleDetails.Add(saleDetails);
                //        }
                //    }
                //}


            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Concurrency error: " + ex.Message);
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

                sale.IsDeleted = true;
                _context.Update(sale);
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
