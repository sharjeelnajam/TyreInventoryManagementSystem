using Domain.DTO;
using Domain.Enums;

namespace Infrastructure.Services
{
    public interface IExcelImportService
    {
        /// <summary>
        /// Imports Excel data into the database based on the specified import type.
        /// </summary>
        /// <param name="fileStream">Excel file stream (e.g. .xlsx)</param>
        /// <param name="importType">Product or Customer</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<ExcelImportResultDto> ImportAsync(Stream fileStream, ExcelImportType importType, CancellationToken cancellationToken = default);
    }
}
