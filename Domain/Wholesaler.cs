using Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Wholesaler : BaseEntity
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Phone { get; set; }

        public string? Email { get; set; }
        public string? Address { get; set; }

        public Guid ListManagementId { get; set; }
    }
}
