using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Supplier : BaseEntity
    {
        [Required(ErrorMessage = "Supplier name is required.")]
        public string Name { get; set; }

        [Phone(ErrorMessage = "Invalid phone number.")]
        [Required(ErrorMessage = "Phone number is required.")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? CompanyName { get; set; }

        public string? TaxNumber { get; set; }  // e.g., NTN or GST number

        public string? ContactPerson { get; set; }

        public string? PaymentTerms { get; set; }  // e.g., "Net 30", "Advance"

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<Purchase> Purchases { get; set; }
    }
}
