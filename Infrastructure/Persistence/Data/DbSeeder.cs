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
            var hasThreadData = await context.ListManagements.AnyAsync(lm => lm.Type == ListType.Size && !lm.IsDeleted);
            if (!hasThreadData)
            {
                var Threads = new List<ListManagement>
                {
                    new ListManagement { Id = Guid.NewGuid(), Name = "14'", Type = ListType.Size, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "15'", Type = ListType.Size, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "16'", Type = ListType.Size, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "17'", Type = ListType.Size, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "18'", Type = ListType.Size, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "19'", Type = ListType.Size, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "20'", Type = ListType.Size, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null }
                };
                await context.ListManagements.AddRangeAsync(Threads);
            }

            // Seed Unit data - TenantId is null for shared reference data
            var hasUnitData = await context.ListManagements.AnyAsync(lm => lm.Type == ListType.Tread && !lm.IsDeleted);
            if (!hasUnitData)
            {
                var units = new List<ListManagement>
                {
                    new ListManagement { Id = Guid.NewGuid(), Name = "5mm", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "5mm+", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "6mm", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "6mm+", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "7mm", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "7mm+", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "8mm", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "8mm+", Type = ListType.Tread, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null }
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

            // Seed Customer Type data (Wholesaler, Customer, Walk-in) - used for unified Customer module
            var hasCustomerTypeData = await context.ListManagements.AnyAsync(lm => lm.Type == ListType.CustomerType && !lm.IsDeleted);
            if (!hasCustomerTypeData)
            {
                var customerTypes = new List<ListManagement>
                {
                    new ListManagement { Id = Guid.NewGuid(), Name = "Wholesaler", Type = ListType.CustomerType, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Customer", Type = ListType.CustomerType, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Walk-In", Type = ListType.CustomerType, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null }
                };
                await context.ListManagements.AddRangeAsync(customerTypes);
            }

            // Seed Shop Service data - common services staff can select when billing
            var hasShopServiceData = await context.ListManagements.AnyAsync(lm => lm.Type == ListType.ShopService && !lm.IsDeleted);
            if (!hasShopServiceData)
            {
                var shopServices = new List<ListManagement>
                {
                    new ListManagement { Id = Guid.NewGuid(), Name = "Puncture", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 15.00m },
                    new ListManagement { Id = Guid.NewGuid(), Name = "New tyres (supply and fit)", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 20.00m },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Wheel balancing", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 12.00m },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Valve replacement", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 5.00m },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Tyre fitting only", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 10.00m },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Tyre removal and disposal", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 8.00m },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Wheel alignment check", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 25.00m },
                    new ListManagement { Id = Guid.NewGuid(), Name = "Other service", Type = ListType.ShopService, IsActive = true, CreatedAt = DateTime.UtcNow, TenantId = null, DefaultPrice = 0.00m }
                };
                await context.ListManagements.AddRangeAsync(shopServices);
            }

            await context.SaveChangesAsync();
        }
    }
}
