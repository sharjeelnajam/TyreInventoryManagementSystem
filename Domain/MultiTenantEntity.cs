using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class MultiTenantEntity : BaseEntity
    {
        public Guid TenantId { get; set; }
    }
}
