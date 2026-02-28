using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Domain
{
  public  class Customer : BaseEntity
    {
        [Required(ErrorMessage = "Customer name is required.")]
        public string Name { get; set; }

        public string Phone { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }

        public string? VehicleNumber { get; set; }

        [Precision(18, 2)]
        public decimal? CreditLimit { get; set; }

        public bool IsActive { get; set; } = true;

        [Precision(18, 2)]
        public decimal? Percentage { get; set; }
        [Precision(18, 2)]
        public decimal? CustomPrice { get; set; }

        [Required(ErrorMessage = "Customer type is required.")]
        public Guid ListManagementId { get; set; }

        // Navigation
        public ICollection<Sale> Sales { get; set; }
    }
}
