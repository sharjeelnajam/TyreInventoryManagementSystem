using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Data
{
   public class ApplicationRole : IdentityRole<Guid>
    {
        public Guid TenantId { get; set; }
    }
}
