using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
   public class PurchaseDetail : BaseEntity
    {
        [Required(ErrorMessage = "PurchaseId is required.")]
        public Guid PurchaseId { get; set; }

        public Purchase Purchase { get; set; }

        [Required(ErrorMessage = "ProductId is required.")]
        public Guid ProductId { get; set; }

        public Product Product { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Unit price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0.")]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "Total price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Total price must be greater than 0.")]
        public decimal TotalPrice { get; set; }

        // Optional Fields
        public string? ProductSize { get; set; } 
        public string? Brand { get; set; }
        public string? Remarks { get; set; }
    }
}
