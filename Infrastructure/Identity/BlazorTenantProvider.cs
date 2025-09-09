using Microsoft.AspNetCore.Components.Authorization;
using Shared.MultiTenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                var authState = _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
                var user = authState.User;
                var claimValue = user?.FindFirst("tid")?.Value;

                if (!string.IsNullOrWhiteSpace(claimValue) && Guid.TryParse(claimValue, out var tid))
                    return tid;

                throw new UnauthorizedAccessException("Tenant could not be resolved from the current user.");
            }
        }
    }
}
