using Domain.Enums;
using Infrastructure.Services;

namespace IMS.Services
{
    public interface IVatSettingsClientService
    {
        Task<VatMode> GetModeAsync();
        Task SetModeAsync(VatMode mode);
    }

    public sealed class VatSettingsClientService : IVatSettingsClientService
    {
        private readonly ITenantService _tenantService;

        public VatSettingsClientService(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        public Task<VatMode> GetModeAsync() => _tenantService.GetVatModeAsync();

        public Task SetModeAsync(VatMode mode) => _tenantService.UpdateVatModeAsync(mode);
    }
}
