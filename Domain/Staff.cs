using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Staff : BaseEntity
    {
        // ✅ Personal Information
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = default!;

        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }   // Male/Female/Other
        public string? CNIC { get; set; }     // Pakistan specific (optional)

        // ✅ Contact Information
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "PhoneNumber is required")]
        public string PhoneNumber { get; set; } = default!;

        public string? Address { get; set; }

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; } = default!;

        public string? Country { get; set; }

        // ✅ Job Information
        public DateTime? HireDate { get; set; }
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public string? StaffCode { get; set; }

        // ✅ Salary Information
        [Required(ErrorMessage = "Salary is required")]
        public decimal Salary { get; set; }

        public decimal? Allowances { get; set; }
        public decimal? Deductions { get; set; }
    }
}
