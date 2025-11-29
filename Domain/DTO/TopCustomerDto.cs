using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTO
{
    public class TopCustomerDto
    {
        public string CustomerName { get; set; } = string.Empty;
        public string WholesalerName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalAmountSpent { get; set; }
    }
}
