using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
   public class ListManagement : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public ListType Type { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
