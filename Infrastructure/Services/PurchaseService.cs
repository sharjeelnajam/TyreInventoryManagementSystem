using Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly ApplicationDbContext _context;

        public PurchaseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Purchase>> GetAllPurchasesAsync()
        {
            return await _context.Purchase
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
                .Where(p => !p.IsDeleted)
                .ToListAsync();
        }

        public async Task<Purchase> GetPurchaseByIdAsync(Guid id)
        {
            var purchase = await _context.Purchase
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (purchase != null)
                return purchase;
            return new Purchase();
        }

        public async Task AddPurchaseAsync(Purchase purchase)
        {
            try
            {
                purchase.Id = Guid.NewGuid();
                purchase.CreatedAt = DateTime.UtcNow;
                var purchaseDetails = new List<PurchaseDetail>();
                if (purchase.PurchaseDetails != null)
                {
                    foreach (var detail in purchase.PurchaseDetails)
                    {
                        detail.Id = Guid.NewGuid();
                        detail.PurchaseId = purchase.Id;
                        detail.TotalPrice = detail.Quantity * detail.UnitPrice;
                        purchaseDetails.Add(detail);
                    }
                }

                _context.Purchase.Add(purchase);
                if (purchaseDetails != null)
                    await _context.PurchaseDetails.AddRangeAsync(purchaseDetails);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task UpdatePurchaseAsync(Purchase purchase)
        {
            try
            {
                var existing = await _context.Purchase
         .Include(p => p.PurchaseDetails)
         .FirstOrDefaultAsync(p => p.Id == purchase.Id);

                if (existing == null) return;

                // Update main fields
                _context.Entry(existing).CurrentValues.SetValues(purchase);

                // Sync details (add / update / delete)
                // 1. Delete those which are not in updated list
                var detailIds = purchase.PurchaseDetails?.Select(d => d.Id).ToList() ?? new List<Guid>();
                var toRemove = existing.PurchaseDetails.Where(d => !detailIds.Contains(d.Id)).ToList();
                foreach (var r in toRemove)
                {
                    _context.PurchaseDetails.Remove(r);
                }

                // 2. Update or Add
                if (purchase.PurchaseDetails != null)
                {
                    foreach (var detail in purchase.PurchaseDetails)
                    {
                        var existingDetail = existing.PurchaseDetails.FirstOrDefault(d => d.Id == detail.Id);

                        if (existingDetail != null)
                        {
                            // update existing
                            _context.Entry(existingDetail).CurrentValues.SetValues(detail);
                            existingDetail.TotalPrice = detail.Quantity * detail.UnitPrice;
                        }
                        else
                        {
                            // add new
                            detail.Id = Guid.NewGuid();
                            detail.PurchaseId = purchase.Id;
                            detail.TotalPrice = detail.Quantity * detail.UnitPrice;
                            existing.PurchaseDetails.Add(detail);
                        }
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


        public async Task DeletePurchaseAsync(Guid id)
        {
            var purchase = await _context.Purchase.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase != null)
            {
                purchase.IsDeleted = true;
                purchase.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
