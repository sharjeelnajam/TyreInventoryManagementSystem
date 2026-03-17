using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTO
{
    public class TodaySaleReportDto
    {
        public string ReferenceNumber { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        /// <summary>Price after discount (net) for this line; proportional to sale net when sale has discount.</summary>
        public decimal NetPrice { get; set; }
        public DateTime SaleDate { get; set; }
    }
}
