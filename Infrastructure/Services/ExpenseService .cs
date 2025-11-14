using Domain;
using Microsoft.EntityFrameworkCore;
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

        public ExpenseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Expense>> GetAllAsync()
        {
            try
            {
                return await _context.Expenses.OrderByDescending(e => e.Date).ToListAsync();
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
                return await _context.Expenses.FirstOrDefaultAsync(e =>e.Id == id);
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
    }
}
