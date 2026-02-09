using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain
{
    public class ShopServiceBill : BaseEntity
    {
        public string BillNumber { get; set; } = string.Empty;

        public ShopServiceBillStatus Status { get; set; } = ShopServiceBillStatus.Open;

        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public Guid? WholesalerId { get; set; }
        public string? WholesalerName { get; set; }

        [NotMapped]
        public Customer? Customer { get; set; }

        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }

        [Precision(18, 2)]
        public decimal Discount { get; set; }

        public string? PaymentMethod { get; set; }
        public string? PaymentStatus { get; set; }
        public string? Notes { get; set; }

        public ICollection<ShopServiceBillItem> Items { get; set; } = new List<ShopServiceBillItem>();
    }
}
