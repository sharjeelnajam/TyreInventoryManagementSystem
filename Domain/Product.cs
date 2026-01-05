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

        public string? DOT { get; set; }

        public string? Brand { get; set; } = default!;

        public string? TyreSize { get; set; } = default!;
       
        public string? Min_Threshold { get; set; }

        public string? Type { get; set; }

        [Precision(18, 2)]
        public decimal AverageCostPrice { get; set; }

        public string? Barcode { get; set; }

        public string? ImagePath { get; set; }

        //basically ListManagementId
        public Guid Unit { get; set; }
        //basically ListManagementId
        public Guid ThreadId { get; set; }

        public ICollection<PurchaseDetail> PurchaseDetails { get; set; }
    }
}
