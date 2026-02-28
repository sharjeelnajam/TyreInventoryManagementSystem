using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IMS.Components;
using IMS.Components.Account;
using Shared.MultiTenancy;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Domain.Identity;
using Infrastructure.Services;
using Infrastructure.Persistence.Data;
using Domain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();

//  Claims Factory (TenantId claim inject karega)
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();

//  Tenant Provider
builder.Services.AddScoped<ITenantProvider, BlazorTenantProvider>();

//  MultiTenant SaveChanges Interceptor
builder.Services.AddScoped<MultiTenantSaveChangesInterceptor>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IProfitReportService, ProfitReportService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IAdminPanalService, AdminPanalService>();
builder.Services.AddScoped<IListManagementService, ListManagementService>();
builder.Services.AddScoped<IShopServiceBillingService, ShopServiceBillingService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IWholesalerPriceService, WholesalerPriceService>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();


builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options => 
    { 
        options.DetailedErrors = true;
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
    });

// Configure Kestrel for larger file uploads (e.g. Excel imports)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});
//  Database connection with interceptor
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(sp.GetRequiredService<MultiTenantSaveChangesInterceptor>());
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//  Identity with ApplicationUser
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddTransient<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // ✅ Call DbSeeder here
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

    await DbSeeder.SeedAsync(userManager, roleManager, db);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
