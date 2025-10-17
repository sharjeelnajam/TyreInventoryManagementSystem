using Microsoft.AspNetCore.Http;
using Shared.MultiTenancy;
using System.Security.Claims;

namespace Infrastructure.Identity
{
    public class BlazorTenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BlazorTenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid TenantId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated == true)
                {
                    if (user.IsInRole("SuperAdmin"))
                        return Guid.Empty; // SuperAdmin can see all tenants

                    var claimValue = user.FindFirst("TenantId")?.Value;

                    if (!string.IsNullOrWhiteSpace(claimValue) && Guid.TryParse(claimValue, out var tid))
                        return tid;
                }

                throw new UnauthorizedAccessException("Tenant could not be resolved from current user.");
            }
        }
    }
}
