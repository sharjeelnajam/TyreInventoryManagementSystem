using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
  public  class SaleDetail : BaseEntity
    {
        [Required(ErrorMessage = "SaleId is required.")]
        public Guid SaleId { get; set; }

        public Sale Sale { get; set; }

        [Required(ErrorMessage = "ProductId is required.")]
        public Guid ProductId { get; set; }

        public Product Product { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Unit price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0.")]
        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "Total price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Total price must be greater than 0.")]
        [Precision(18, 2)]
        public decimal TotalPrice { get; set; }

        // store cost at time of sale (so historical profits are stable)
        [Precision(18, 2)]
        public decimal CostPrice { get; set; }

        // profit for this sale detail: (UnitPrice - CostPrice) * Quantity
        [Precision(18, 2)]
        public decimal ProfitAmount { get; set; }

        // Optional / Useful Fields
        public string? ProductSize { get; set; }   // e.g., "195/65 R15"
        public string? Brand { get; set; }         // e.g., "Michelin"
        public string? Remarks { get; set; }

        /// <summary>
        /// Optional label for this sale line only (invoices/receipts). Does not change the catalog product name.
        /// </summary>
        [MaxLength(512)]
        public string? LineDisplayName { get; set; }
    }
}
