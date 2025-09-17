using Microsoft.AspNetCore.Components.Authorization;
using Shared.MultiTenancy;
using System.Security.Claims;

namespace Infrastructure.Identity
{
    public class BlazorTenantProvider : ITenantProvider
    {
        private readonly AuthenticationStateProvider _authStateProvider;

        public BlazorTenantProvider(AuthenticationStateProvider authStateProvider)
        {
            _authStateProvider = authStateProvider;
        }

        public Guid TenantId
        {
            get
            {
                var authState = _authStateProvider.GetAuthenticationStateAsync()
                                                  .ConfigureAwait(false)
                                                  .GetAwaiter()
                                                  .GetResult();

                ClaimsPrincipal user = authState.User;

                if (user?.Identity?.IsAuthenticated == true)
                {
                    var claimValue = user.FindFirst("TenantId")?.Value;

                    if (!string.IsNullOrWhiteSpace(claimValue) && Guid.TryParse(claimValue, out var tid))
                        return tid;
                }

                throw new UnauthorizedAccessException("Tenant could not be resolved from the current user.");
            }
        }
    }
}
