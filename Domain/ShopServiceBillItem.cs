using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Domain
{
    public class ShopServiceBillItem : BaseEntity
    {
        public Guid ShopServiceBillId { get; set; }
        public ShopServiceBill ShopServiceBill { get; set; } = null!;

        /// <summary>Reference to ListManagement (Type = ShopService).</summary>
        public Guid? ServiceId { get; set; }
        /// <summary>When selling a product (tyre), links to Product for stock update.</summary>
        public Guid? ProductId { get; set; }
        public string ServiceName { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;

        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        [Precision(18, 2)]
        public decimal TotalPrice { get; set; }

        public string? Remarks { get; set; }
    }
}
