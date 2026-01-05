using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class WholeSalerPrice : BaseEntity
    {
        public Guid WholesalerId { get; set; }

        public Guid ThreadId { get; set; }   // ListManagement (Thread)
        public Guid UnitId { get; set; }   // ListManagement (Unit)

        public decimal Price { get; set; }

        // Navigation (optional)
        public Wholesaler Wholesaler { get; set; }
    }
}
