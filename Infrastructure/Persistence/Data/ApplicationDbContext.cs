using Domain;
using Domain.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System.Linq.Expressions;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly MultiTenantSaveChangesInterceptor _tenantInterceptor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
        ITenantProvider tenantProvider,
        MultiTenantSaveChangesInterceptor tenantInterceptor)
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _tenantInterceptor = tenantInterceptor;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Global filter: IsDeleted == false
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");

                var isDeletedFilter = Expression.Equal(
                    Expression.Property(parameter, nameof(BaseEntity.IsDeleted)),
                    Expression.Constant(false)
                );

                var lambda = Expression.Lambda(isDeletedFilter, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    // ✅ DbSets
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Staff> Staff { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Supplier> Supplier { get; set; }
    public DbSet<Customer> Customer { get; set; }
    public DbSet<Purchase> Purchase { get; set; }
    public DbSet<Sale> Sale { get; set; }
    public DbSet<SaleDetail> SaleDetail { get; set; }
    public DbSet<PurchaseDetail> PurchaseDetails { get; set; }
    public DbSet<StockHistory> StockHistories { get; set; }
    public DbSet<ProfitHistory> ProfitHistories { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<ListManagement> ListManagements { get; set; }
    public DbSet<CustomerSalerPrice> CustomerSalerPrices { get; set; }
    public DbSet<ShopServiceBill> ShopServiceBills { get; set; }
    public DbSet<ShopServiceBillItem> ShopServiceBillItems { get; set; }

}
