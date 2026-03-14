using Shared.MultiTenancy;

namespace Infrastructure.Identity
{
    public class SuperAdminBranchService : ISuperAdminBranchService
    {
        public Guid? SelectedBranchId { get; set; }
    }
}
