using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
  public  class Sale : BaseEntity
    {
        public string? SaleNumber { get; set; }

        [Required(ErrorMessage = "Sale date is required.")]
        public DateTime SaleDate { get; set; }

        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Customer Customer { get; set; }

        [Required(ErrorMessage = "Total amount is required.")]
        public decimal TotalAmount { get; set; }

        public decimal? Discount { get; set; }
        public decimal? TaxAmount { get; set; }

        [Required(ErrorMessage = "Net amount is required.")]
        public decimal NetAmount { get; set; }

        [Required(ErrorMessage = "Payment status is required.")]
        public string PaymentStatus { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        public string PaymentMethod { get; set; }

        public DateTime? DueDate { get; set; }

        [Required(ErrorMessage = "Approval status is required.")]
        public bool IsApproved { get; set; }

        public string? Notes { get; set; }
        public string? InvoiceFilePath { get; set; }
        public string? VehicleNumber { get; set; }
        public bool IsReturn { get; set; } 
        public int? ReturnedAgainstSaleId { get; set; }
        public string? SalespersonName { get; set; }
        public int? WarehouseId { get; set; }

        public ICollection<SaleDetail> SaleDetails { get; set; }
    }
}
