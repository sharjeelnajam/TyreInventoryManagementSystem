using Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class Expense : BaseEntity
    {
        [Required(ErrorMessage = "Expense Title is required")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(1, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Category is required")]
        public ExpenseCategory Category { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;
        
        public string? UploadedFile { get; set; }
        public string? Description { get; set; }

    }
}
