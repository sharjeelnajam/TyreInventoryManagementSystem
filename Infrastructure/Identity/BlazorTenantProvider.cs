using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Shared.MultiTenancy;
using System.Security.Claims;

namespace Infrastructure.Identity
{
    public class BlazorTenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly ISuperAdminBranchService? _superAdminBranchService;

        public BlazorTenantProvider(
            IHttpContextAccessor httpContextAccessor,
            AuthenticationStateProvider authenticationStateProvider,
            ISuperAdminBranchService? superAdminBranchService = null)
        {
            _httpContextAccessor = httpContextAccessor;
            _authenticationStateProvider = authenticationStateProvider;
            _superAdminBranchService = superAdminBranchService;
        }

        public Guid TenantId
        {
            get
            {
                var user = GetCurrentUser();

                if (user?.Identity?.IsAuthenticated == true)
                {
                    if (user.IsInRole("SuperAdmin"))
                    {
                        // Branch can come from query string (survives full page reload) or from in-memory service
                        var queryBranch = _httpContextAccessor.HttpContext?.Request?.Query["branch"].FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(queryBranch) && Guid.TryParse(queryBranch, out var branchId))
                        {
                            if (_superAdminBranchService != null)
                                _superAdminBranchService.SelectedBranchId = branchId;
                            return branchId;
                        }
                        var selected = _superAdminBranchService?.SelectedBranchId;
                        return selected ?? Guid.Empty;
                    }

                    var claimValue = user.FindFirst("TenantId")?.Value
                        ?? user.FindFirst("tid")?.Value;

                    if (!string.IsNullOrWhiteSpace(claimValue) && Guid.TryParse(claimValue, out var tid))
                        return tid;
                }

                throw new UnauthorizedAccessException("Tenant could not be resolved from current user.");
            }
        }

        private ClaimsPrincipal? GetCurrentUser()
        {
            var httpUser = _httpContextAccessor.HttpContext?.User;
            if (httpUser?.Identity?.IsAuthenticated == true)
                return httpUser;

            var authState = _authenticationStateProvider
                .GetAuthenticationStateAsync()
                .GetAwaiter()
                .GetResult();

            return authState.User;
        }
    }
}
