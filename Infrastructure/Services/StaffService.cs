using Domain;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class StaffService : IStaffService
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantProvider _tenantProvider;


        public StaffService(ApplicationDbContext context, RoleManager<ApplicationRole> roleManager,
            UserManager<ApplicationUser> userManager
            , ITenantProvider tenantProvider)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
            _tenantProvider = tenantProvider;
        }

        // CREATE: Add a new staff member
        public async Task<bool> AddStaffAsync(Staff staff)
        {
            try
            {
                _context.Staff.Add(staff);
                await _context.SaveChangesAsync();

                var staffRegister = new ApplicationUser
                {
                    UserName = staff.Email,
                    Email = staff.Email,
                    TenantId = _tenantProvider.TenantId,   // yahan Tenant assign kiya
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(staffRegister, "Staff@123");

                if (!result.Succeeded)
                    throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

                // 4. Role ensure karo (agar Admin role exist nahi karta to create karo)
                var existRole = _roleManager.Roles.FirstOrDefault(x => x.Name == "Staff" && x.TenantId == _tenantProvider.TenantId);
                if (existRole == null)
                    await _roleManager.CreateAsync(new ApplicationRole { Name = "Staff", TenantId = _tenantProvider.TenantId });

                existRole = _roleManager.Roles.FirstOrDefault(x => x.Name == "Staff" && x.TenantId == _tenantProvider.TenantId); // 5. User ko Admin role do await _userManager.AddToRoleAsync(staf, "Staff",);

                // 5. User ko Admin role do
                _context.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = staffRegister.Id,
                    RoleId = existRole.Id
                });
                await _context.SaveChangesAsync();
                return true; 
            }
            catch (Exception ex)
            {
                return false; // Error occurred
            }
        }

        // READ: Get all staff members (only those who are not deleted)
        public async Task<List<Staff>> GetAllStaffAsync()
        {
            try
            {
                return await _context.Staff
                    .Where(s => s.IsDeleted == false)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<Staff>(); // Return an empty list in case of error
            }
        }

        // READ: Get a specific staff member by ID
        public async Task<Staff?> GetStaffByIdAsync(Guid staffId)
        {
            try
            {
                return await _context.Staff
                    .Where(s => s.Id == staffId && s.IsDeleted == false)
                    .FirstOrDefaultAsync();
            }
            catch (Exception)
            {
                return null; // Return null in case of error
            }
        }

        // UPDATE: Update staff details
        public async Task<bool> UpdateStaffAsync(Staff updatedStaff)
        {
            try
            {
                var staff = await _context.Staff.FindAsync(updatedStaff.Id);
                if (staff == null || staff.IsDeleted)
                {
                    return false; // Staff not found or is deleted
                }

                staff.Name = updatedStaff.Name;
                staff.DateOfBirth = updatedStaff.DateOfBirth;
                staff.Gender = updatedStaff.Gender;
                staff.CNIC = updatedStaff.CNIC;
                staff.Email = updatedStaff.Email;
                staff.PhoneNumber = updatedStaff.PhoneNumber;
                staff.Address = updatedStaff.Address;
                staff.City = updatedStaff.City;
                staff.Country = updatedStaff.Country;
                staff.HireDate = updatedStaff.HireDate;
                staff.JobTitle = updatedStaff.JobTitle;
                staff.Department = updatedStaff.Department;
                staff.StaffCode = updatedStaff.StaffCode;
                staff.Salary = updatedStaff.Salary;
                staff.Allowances = updatedStaff.Allowances;
                staff.Deductions = updatedStaff.Deductions;

                _context.Staff.Update(staff);
                await _context.SaveChangesAsync();
                return true; // Successfully updated
            }
            catch (Exception)
            {
                return false; // Error occurred
            }
        }

        // DELETE: Soft delete a staff member by updating IsDeleted
        public async Task<bool> DeleteAsync(Guid staffId)
        {
            try
            {
                var staff = await _context.Staff.FindAsync(staffId);
                if (staff == null || staff.IsDeleted)
                {
                    return false; // Staff not found or already deleted
                }

                staff.IsDeleted = true;  // Mark the staff as deleted
                _context.Staff.Update(staff);
                await _context.SaveChangesAsync();
                return true; // Successfully deleted (soft delete)
            }
            catch (Exception)
            {
                return false; // Error occurred
            }
        }
    }
}
