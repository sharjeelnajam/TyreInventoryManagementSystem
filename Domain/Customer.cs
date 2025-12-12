using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
  public  class Customer : BaseEntity
    {
        [Required(ErrorMessage = "Customer name is required.")]
        public string Name { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [Required(ErrorMessage = "Phone number is required.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

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
        public CustomerType CustomerType { get; set; }

        // Navigation
        public ICollection<Sale> Sales { get; set; }
    }
}
