using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class StockHistory : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        [Required]
        public string ActionType { get; set; } // "Purchase" | "Sale" | "Adjustment"

        [Required]
        public int QuantityChanged { get; set; } // +10 for purchase, -5 for sale

        [Required]
        public int NewStockLevel { get; set; } // Stock level after the action

        [Required]
        public DateTime ActionDate { get; set; } = DateTime.Now;

        [Required]
        public int PreviousStockLevel { get; set; } // Stock before the action

        public Guid? ReferenceId { get; set; } // PurchaseId, SaleId, etc.

        public string? ReferenceNumber { get; set; } // PurchaseNumber or SaleNumber
        public string? PerformedBy { get; set; } // Optional: who performed the action
    }
}
