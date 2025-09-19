using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IStaffService
    {
        Task<bool> AddStaffAsync(Staff staff);
        Task<List<Staff>> GetAllStaffAsync();
        Task<Staff?> GetStaffByIdAsync(Guid staffId);
        Task<bool> UpdateStaffAsync(Staff updatedStaff);
        Task<bool> DeleteAsync(Guid staffId);
    }
}
