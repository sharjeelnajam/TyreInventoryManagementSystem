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

        [Required(ErrorMessage = "Tyre size is required")]
        public string TyreSize { get; set; } = default!;

        public decimal? TreadDepth { get; set; }  // mm

        [Required(ErrorMessage = "Purchase price is required")]
        public decimal PurchasePrice { get; set; }

        [Required(ErrorMessage = "Selling price is required")]
        public decimal SellingPrice { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        public int Quantity { get; set; }

        public string? Barcode { get; set; }
    }
}
