using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
   public class Purchase : BaseEntity
    {
        [Required(ErrorMessage = "Purchase number is required.")]
        public string PurchaseNumber { get; set; }

        [Required(ErrorMessage = "Purchase date is required.")]
        public DateTime PurchaseDate { get; set; }

        [Required(ErrorMessage = "Supplier is required.")]
        public Guid SupplierId { get; set; }

        public Supplier Supplier { get; set; }

        [Required(ErrorMessage = "Total amount is required.")]
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "Net amount is required.")]
        public decimal NetAmount { get; set; }

        [Required(ErrorMessage = "Payment status is required.")]
        public string PaymentStatus { get; set; }

        [Required(ErrorMessage = "Approval status is required.")]
        public bool IsApproved { get; set; }

        // Optional Fields – no validation required
        public decimal? Discount { get; set; }
        public decimal? TaxAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? DueDate { get; set; }

        public ICollection<PurchaseDetail> PurchaseDetails { get; set; }
    }
}
