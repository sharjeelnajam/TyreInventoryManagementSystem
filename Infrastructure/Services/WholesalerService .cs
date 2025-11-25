using Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
  
        public class WholesalerService : IWholesalerService
        {
            private readonly ApplicationDbContext _context;

            public WholesalerService(ApplicationDbContext context)
            {
                _context = context;
            }

            public async Task<List<Wholesaler>> GetAllAsync()
            {
                try
                {
                    return await _context.Wholesalers.ToListAsync();
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine($"GetAllAsync Error: {ex.Message}");
                    return new List<Wholesaler>();
                }
            }

            public async Task<Wholesaler?> GetByIdAsync(Guid id)
            {
                try
                {
                    if (id == Guid.Empty)
                        return null;

                    return await _context.Wholesalers
                        .FirstOrDefaultAsync(x => x.Id == id);
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine($"GetByIdAsync Error: {ex.Message}");
                    return null;
                }
            }

            public async Task<bool> AddAsync(Wholesaler wholesaler)
            {
                try
                {
                    if (wholesaler == null)
                        return false;

                    await _context.Wholesalers.AddAsync(wholesaler);
                    return await _context.SaveChangesAsync() > 0;
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine($"AddAsync Error: {ex.Message}");
                    return false;
                }
            }

            public async Task<bool> UpdateAsync(Wholesaler wholesaler)
            {
                try
                {
                    if (wholesaler == null || wholesaler.Id == Guid.Empty)
                        return false;

                    _context.Wholesalers.Update(wholesaler);
                    return await _context.SaveChangesAsync() > 0;
                }
                catch (Exception ex)
                {
                
                    Console.WriteLine($"UpdateAsync Error: {ex.Message}");
                    return false;
                }
            }

            public async Task<bool> DeleteAsync(Guid id)
            {
                try
                {
                    if (id == Guid.Empty)
                        return false;

                    var entity = await _context.Wholesalers.FindAsync(id);
                    if (entity == null)
                        return false;

                    _context.Wholesalers.Remove(entity);
                    return await _context.SaveChangesAsync() > 0;
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine($"DeleteAsync Error: {ex.Message}");
                    return false;
                }
            }
        }
    
}
