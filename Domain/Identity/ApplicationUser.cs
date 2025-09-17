using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        // Add TenantId so each user belongs to a tenant
        [ForeignKey(nameof(Tenant))]
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
