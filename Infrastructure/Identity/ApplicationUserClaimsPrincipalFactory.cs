using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Identity
{
  public  class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
    {
        public ApplicationUserClaimsPrincipalFactory(
       UserManager<ApplicationUser> userManager,
       RoleManager<ApplicationRole> roleManager,
       IOptions<IdentityOptions> optionsAccessor)
       : base(userManager, roleManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // TenantId ko claim me inject karna
            if (user.TenantId != Guid.Empty)
            {
                identity.AddClaim(new Claim("TenantId", user.TenantId.ToString()));
            }

            return identity;
        }
    }
}
