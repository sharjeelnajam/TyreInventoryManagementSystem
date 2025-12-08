using Domain;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
   public class ExpenseService : IExpenseService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ExpenseService(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<Expense>> GetAllAsync()
        {
            try
            {
                if (_tenantProvider.TenantId != Guid.Empty)
                {

                    return await _context.Expenses.Where(e => e.TenantId == _tenantProvider.TenantId).OrderByDescending(e => e.Date).ToListAsync();
                }
                else return new List<Expense>();
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        public async Task<Expense> GetByIdAsync(Guid id)
        {
            try
            {
                if(_tenantProvider.TenantId != Guid.Empty)
                {
                    return await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == _tenantProvider.TenantId);

                }
                return new Expense();
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public async Task AddAsync(Expense expense)
        {
            try
            {
                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        public async Task UpdateAsync(Expense expense)
        {
            try
            {

                var existing = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == expense.Id);

                if (existing == null)
                    throw new Exception("Expense not found.");

                existing.Amount = expense.Amount;
                existing.Description = expense.Description;
                existing.Category = expense.Category;
                existing.UpdatedAt = DateTime.Now;
                existing.Title = expense.Title;
                existing.UploadedFile = expense.UploadedFile;
                existing.OrignalFileName = expense.OrignalFileName;
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        public async Task DeleteAsync(Guid id)
        {
            try
            {
                var exp = await _context.Expenses.FindAsync(id);
                if (exp != null)
                {
                    _context.Expenses.Remove(exp);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception)
            {

                throw;
            }
        
        }

        public async Task<decimal> GetProfitAsync(DateTime start, DateTime end)
        {
            try
            {
                if(_tenantProvider.TenantId != Guid.Empty)
                {
                    return await _context.Expenses
                       .Where(p => p.Date >= start && p.Date < end && p.TenantId == _tenantProvider.TenantId)
                       .SumAsync(p => p.Amount);
                }
                return 0m;

            }
            catch (Exception)
            {
                throw;
            }
        }

        public Task<decimal> GetMonthExpenseAsync()
        {
            try
            {
                var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var end = start.AddMonths(1);
                return GetProfitAsync(start, end);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<decimal> GetProfitByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Include the end date in the range by adding one day and using less than
                var adjustedEndDate = endDate.AddDays(1);
                if(_tenantProvider.TenantId != Guid.Empty)
                {
                    return await _context.Expenses
                       .Where(p => p.Date >= startDate && p.Date < adjustedEndDate && p.TenantId == _tenantProvider.TenantId)
                       .SumAsync(p => p.Amount);
                }

                return 0m;
                
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
