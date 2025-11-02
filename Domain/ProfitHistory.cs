using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class ProfitHistory : BaseEntity
    {
        // Link to sale and sale detail if you want (nullable for flexibility)
        public Guid? SaleId { get; set; }
        public Guid? SaleDetailId { get; set; }

        [Required]
        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        [Precision(18, 2)]
        public decimal CostPrice { get; set; }

        [Precision(18, 2)]
        public decimal SellingPrice { get; set; }

        public int Quantity { get; set; }

        [Precision(18, 2)]
        public decimal ProfitAmount { get; set; }

        [Required]
        public DateTime RecordedAt { get; set; } = DateTime.Now;

        // Optional: who recorded it
        public string? PerformedBy { get; set; }

        // Optional reference numbers
        public string? ReferenceNumber { get; set; } // SaleNumber or PurchaseNumber
    }
}
