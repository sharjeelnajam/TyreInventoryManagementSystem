using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTO
{
    public class TopSupplierDto
    {
        public string SupplierName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalAmountPaid { get; set; }
    }
}
