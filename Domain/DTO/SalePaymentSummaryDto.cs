using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTO
{
    public class SalePaymentSummaryDto
    {
        public decimal TotalCash { get; set; }
        public decimal TotalCard { get; set; }
    }
}

