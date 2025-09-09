using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Data
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        // Add TenantId so each user belongs to a tenant
        public Guid TenantId { get; set; }
    }
}
