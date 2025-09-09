using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public abstract class TenantEntity
    {
        public Guid TenantId { get; set; }
    }
}
