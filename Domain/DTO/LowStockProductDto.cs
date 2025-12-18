using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTO
{
    public class LowStockProductDto
    {
        public string ProductName { get; set; }
        public int CurrentStock { get; set; }
        public int Threshold { get; set; }
        public string Status { get; set; }
    }
}
