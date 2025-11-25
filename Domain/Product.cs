using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Product : BaseEntity
    {
        [Required(ErrorMessage = "Product name is required")]
        public string ProductName { get; set; } = default!;

        public string? Description { get; set; }

        [Display(Name = "Date of Manufacture")]
        public DateTime? DOT { get; set; }

        [Required(ErrorMessage = "Brand is required")]
        public string Brand { get; set; } = default!;

        public string? TyreSize { get; set; } = default!;

        [Precision(18, 2)]
        public decimal? TreadDepth { get; set; }  // mm

        [Required(ErrorMessage = "Purchase price is required")]
        [Precision(18, 2)]
        public decimal PurchasePrice { get; set; }

        [Required(ErrorMessage = "Selling price is required")]
        [Precision(18, 2)]
        public decimal SellingPrice { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Product Type is required")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Product Thread is required")]
        public string Thread { get; set; }

        [Precision(18, 2)]
        public decimal AverageCostPrice { get; set; }

        public string? Barcode { get; set; }

        public string? ImagePath { get; set; }

        public ICollection<PurchaseDetail> PurchaseDetails { get; set; }
    }
}
