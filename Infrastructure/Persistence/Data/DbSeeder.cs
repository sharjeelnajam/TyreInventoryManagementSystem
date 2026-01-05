using Domain;
using Domain.Enums;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(UserManager<ApplicationUser> userManager,
                                           RoleManager<ApplicationRole> roleManager,
                                           ApplicationDbContext context)
        {
            // Apply migrations if not applied
            await context.Database.MigrateAsync();

            // ✅ Check and create SuperAdmin role
            const string roleName = "SuperAdmin";
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new ApplicationRole
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpper()
                };
                await roleManager.CreateAsync(role);
            }

            // ✅ Check and create SuperAdmin user
            const string superAdminEmail = "superadmin@system.com";
            var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);
            if (superAdmin == null)
            {
                var user = new ApplicationUser
                {
                    UserName = superAdminEmail,
                    Email = superAdminEmail,
                    EmailConfirmed = true,
                    TenantId = null // SuperAdmin has no tenant
                };

                var result = await userManager.CreateAsync(user, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, roleName);
                }
            }

            // ✅ Seed ListManagement data (Thread, Unit, Expense)
            await SeedListManagementDataAsync(context);
        }

        private static async Task SeedListManagementDataAsync(ApplicationDbContext context)
        {
            // Seed Thread data - TenantId is null for shared reference data
            var hasThreadData = await context.ListManagements.AnyAsync(lm => lm.Type == ListType.Thread && !lm.IsDeleted);
            if (!hasThreadData)
            {
                var Threads = new List<ListManagement>
                {
                    new ListManagement { Id = Guid.NewGuid(), Name = "14'", Type = ListType.Thread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "15'", Type = ListType.Thread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "16'", Type = ListType.Thread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "17'", Type = ListType.Thread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "18'", Type = ListType.Thread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "19'", Type = ListType.Thread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "20'", Type = ListType.Thread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null }
                };
                await context.ListManagements.AddRangeAsync(Threads);
            }

            // Seed Unit data - TenantId is null for shared reference data
            var hasUnitData = await context.ListManagements.AnyAsync(lm => lm.Type == ListType.Unit && !lm.IsDeleted);
            if (!hasUnitData)
            {
                var units = new List<ListManagement>
                {
                    new ListManagement { Id = Guid.NewGuid(), Name = "5mm", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "5mm+", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "6mm", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "6mm+", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "7mm", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "7mm+", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "8mm", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "8mm+", Type = ListType.Unit, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null }
                };
                await context.ListManagements.AddRangeAsync(units);
            }

            // Seed Expense data - TenantId is null for shared reference data
            var hasExpenseData = await context.ListManagements.AnyAsync(lm => lm.Type == ListType.Expense && !lm.IsDeleted);
            if (!hasExpenseData)
            {
                var expenses = new List<ListManagement>
                {
                    new ListManagement { Id = Guid.NewGuid(), Name = "Transportation", Type = ListType.Expense, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Regular expense", Type = ListType.Expense, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Furniture", Type = ListType.Expense, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null }
                };
                await context.ListManagements.AddRangeAsync(expenses);
            }

            await context.SaveChangesAsync();
        }
    }
}
