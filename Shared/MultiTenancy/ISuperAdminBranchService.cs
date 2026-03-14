using System;

namespace Shared.MultiTenancy
{
    /// <summary>
    /// Holds the selected branch (tenant) when the current user is Super Admin.
    /// When null or Guid.Empty, Super Admin sees data for all branches.
    /// When set to a tenant id, data is filtered to that branch.
    /// </summary>
    public interface ISuperAdminBranchService
    {
        Guid? SelectedBranchId { get; set; }
    }
}
