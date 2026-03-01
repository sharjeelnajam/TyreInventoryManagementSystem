using ClosedXML.Excel;
using Domain;
using Domain.DTO;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.MultiTenancy;
using System.Globalization;
using System.Linq;

namespace Infrastructure.Services
{
    public class ExcelImportService : IExcelImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ExcelImportService(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<ExcelImportResultDto> ImportAsync(Stream fileStream, ExcelImportType importType, CancellationToken cancellationToken = default)
        {
            var result = new ExcelImportResultDto();
            var tenantId = _tenantProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                result.Success = false;
                result.Message = "Tenant context is required for import.";
                return result;
            }

            try
            {
                using var workbook = new XLWorkbook(fileStream);
                IXLWorksheet? worksheet = null;
                if (importType == ExcelImportType.Product)
                {
                    worksheet = workbook.Worksheets
                        .FirstOrDefault(ws => string.Equals(ws.Name, "Product", StringComparison.OrdinalIgnoreCase));
                    if (worksheet == null)
                    {
                        result.Success = false;
                        result.Message = "Worksheet 'Product' not found in Excel file.";
                        return result;
                    }
                }
                else
                {
                    // Fallback to first sheet for non-product imports (customer)
                    worksheet = workbook.Worksheet(1);
                }

                var rows = worksheet.RowsUsed().Skip(1).ToList(); // skip header
                result.TotalRows = rows.Count;

                if (importType == ExcelImportType.Product)
                    await ImportProductsAsync(rows, worksheet, tenantId, result, cancellationToken);
                else if (importType == ExcelImportType.Customer)
                    await ImportCustomersAsync(rows, worksheet, tenantId, result, cancellationToken);
                else
                {
                    result.Success = false;
                    result.Message = $"Unsupported import type: {importType}";
                    return result;
                }

                result.Success = result.ErrorCount == 0;
                result.Message ??= result.Success
                    ? $"Imported {result.ImportedCount} of {result.TotalRows} rows."
                    : $"Imported {result.ImportedCount} of {result.TotalRows} rows. {result.ErrorCount} error(s).";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Import failed: {ex.Message}";
                result.RowErrors.Add(new ExcelImportRowError { RowNumber = 0, Error = ex.Message });
            }

            return result;
        }

        private static int GetColumnIndex(IXLWorksheet ws, string headerName)
        {
            var firstRow = ws.FirstRow();
            var lastUsed = firstRow.LastCellUsed();
            int lastCol = lastUsed?.Address.ColumnNumber ?? 0;
            for (int c = 1; c <= lastCol; c++)
            {
                var cellValue = firstRow.Cell(c).GetString().Trim();
                if (string.Equals(cellValue, headerName, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return -1;
        }

        private async Task ImportProductsAsync(
            List<IXLRow> rows,
            IXLWorksheet worksheet,
            Guid tenantId,
            ExcelImportResultDto result,
            CancellationToken cancellationToken)
        {
            int colProductName = GetColumnIndex(worksheet, "ProductName");
            int colDescription = GetColumnIndex(worksheet, "Description");
            int colDOT = GetColumnIndex(worksheet, "DOT");
            int colBrand = GetColumnIndex(worksheet, "Brand");
            int colMin_Threshold = GetColumnIndex(worksheet, "Min_Threshold");
            int colType = GetColumnIndex(worksheet, "Type");
            int colAverageCostPrice = GetColumnIndex(worksheet, "AverageCostPrice");
            int colUnit = GetColumnIndex(worksheet, "Unit");
            int colThread = GetColumnIndex(worksheet, "Thread");
            int colQuantity = GetColumnIndex(worksheet, "Quantity");

            if (colProductName <= 0)
            {
                result.Message = "Required column 'ProductName' not found in Excel.";
                result.ErrorCount++;
                return;
            }
            if (colUnit <= 0 || colThread <= 0)
            {
                result.Message = "Required columns 'Unit' and 'Thread' (ListManagement names) must exist in Excel.";
                result.ErrorCount++;
                return;
            }

            var listManagementItems = await _context.ListManagements
                .Where(x => x.Type == ListType.Tread || x.Type == ListType.Size)
                .ToListAsync(cancellationToken);

            var unitLookup = listManagementItems
                .Where(x => x.Type == ListType.Tread)
                .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var threadLookup = listManagementItems
                .Where(x => x.Type == ListType.Size)
                .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var productsToAdd = new List<Product>();
            var stockHistoriesToAdd = new List<StockHistory>();
            int rowNum = 2; // 1-based, row 1 = header
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? productName = GetCellString(row, colProductName);
                if (string.IsNullOrWhiteSpace(productName))
                {
                    result.RowErrors.Add(new ExcelImportRowError { RowNumber = rowNum, Error = "ProductName is required.", Value = rowNum.ToString() });
                    result.ErrorCount++;
                    rowNum++;
                    continue;
                }

                var unitName = GetCellString(row, colUnit);
                if (string.IsNullOrWhiteSpace(unitName) || !unitLookup.TryGetValue(unitName.Trim(), out var unitId))
                {
                    result.RowErrors.Add(new ExcelImportRowError { RowNumber = rowNum, Error = "Unit name not found in ListManagement.", Value = unitName });
                    result.ErrorCount++;
                    rowNum++;
                    continue;
                }

                var threadName = GetCellString(row, colThread);
                if (string.IsNullOrWhiteSpace(threadName) || !threadLookup.TryGetValue(threadName.Trim(), out var threadId))
                {
                    result.RowErrors.Add(new ExcelImportRowError { RowNumber = rowNum, Error = "Thread name not found in ListManagement.", Value = threadName });
                    result.ErrorCount++;
                    rowNum++;
                    continue;
                }

                decimal averageCostPrice = 0;
                if (colAverageCostPrice > 0)
                {
                    var acp = GetCellString(row, colAverageCostPrice);
                    if (!string.IsNullOrWhiteSpace(acp) && !decimal.TryParse(acp, NumberStyles.Any, CultureInfo.InvariantCulture, out averageCostPrice))
                    {
                        result.RowErrors.Add(new ExcelImportRowError { RowNumber = rowNum, Error = "AverageCostPrice must be a number.", Value = acp });
                        result.ErrorCount++;
                        rowNum++;
                        continue;
                    }
                }

                int quantity = 0;
                if (colQuantity > 0)
                {
                    var qtyStr = GetCellString(row, colQuantity);
                    if (!string.IsNullOrWhiteSpace(qtyStr))
                    {
                        if (!int.TryParse(qtyStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity < 0)
                        {
                            result.RowErrors.Add(new ExcelImportRowError { RowNumber = rowNum, Error = "Quantity must be a non-negative integer.", Value = qtyStr });
                            result.ErrorCount++;
                            rowNum++;
                            continue;
                        }
                    }
                }

                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    ProductName = productName.Trim(),
                    Description = colDescription > 0 ? GetCellString(row, colDescription) : null,
                    DOT = colDOT > 0 ? GetCellString(row, colDOT) : null,
                    Brand = colBrand > 0 ? GetCellString(row, colBrand) : null,
                    Min_Threshold = colMin_Threshold > 0 ? GetCellString(row, colMin_Threshold) : null,
                    Type = colType > 0 ? GetCellString(row, colType) : null,
                    AverageCostPrice = averageCostPrice,
                    Unit = unitId,
                    ThreadId = threadId
                };
                productsToAdd.Add(product);

                if (quantity > 0)
                {
                    var stockHistory = new StockHistory
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ActionType = "Import",
                        QuantityChanged = quantity,
                        PreviousStockLevel = 0,
                        NewStockLevel = quantity,
                        ActionDate = DateTime.UtcNow,
                        ReferenceNumber = "ExcelImport"
                    };
                    stockHistoriesToAdd.Add(stockHistory);
                }

                result.ImportedCount++;
                rowNum++;
            }

            // Only persist to the database if ALL rows are valid
            if (productsToAdd.Count > 0 && result.ErrorCount == 0)
            {
                await _context.Products.AddRangeAsync(productsToAdd, cancellationToken);

                if (stockHistoriesToAdd.Count > 0)
                {
                    await _context.StockHistories.AddRangeAsync(stockHistoriesToAdd, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            else if (result.ErrorCount > 0)
            {
                // We validated some rows successfully but are treating the import as atomic.
                // Since we are not saving anything, reflect that no rows were actually imported.
                result.ImportedCount = 0;
            }
        }

        private async Task ImportCustomersAsync(
            List<IXLRow> rows,
            IXLWorksheet worksheet,
            Guid tenantId,
            ExcelImportResultDto result,
            CancellationToken cancellationToken)
        {
            int colName = GetColumnIndex(worksheet, "Name");
            int colType = GetColumnIndex(worksheet, "Type");
            int colPhone = GetColumnIndex(worksheet, "Phone");
            int colAddress = GetColumnIndex(worksheet, "Address");
            int colCity = GetColumnIndex(worksheet, "City");
            int colVehicleNumber = GetColumnIndex(worksheet, "VehicleNumber");
            int colCreditLimit = GetColumnIndex(worksheet, "CreditLimit");
            int colIsActive = GetColumnIndex(worksheet, "IsActive");
            int colPercentage = GetColumnIndex(worksheet, "Percentage");
            int colCustomPrice = GetColumnIndex(worksheet, "CustomPrice");

            if (colName <= 0)
            {
                result.Message = "Required column 'Name' not found in Excel.";
                result.ErrorCount++;
                return;
            }
            if (colType <= 0)
            {
                result.Message = "Required column 'Type' (customer type name) not found in Excel.";
                result.ErrorCount++;
                return;
            }

            // Load ListManagement items of type CustomerType and build name -> Id lookup
            var customerTypeItems = await _context.ListManagements
                .Where(x => x.Type == ListType.CustomerType)
                .ToListAsync(cancellationToken);
            var typeNameToId = customerTypeItems
                .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var customersToAdd = new List<Customer>();
            int rowNum = 2;
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? name = GetCellString(row, colName);
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.RowErrors.Add(new ExcelImportRowError { RowNumber = rowNum, Error = "Name is required.", Value = rowNum.ToString() });
                    result.ErrorCount++;
                    rowNum++;
                    continue;
                }
                string? typeName = GetCellString(row, colType);
                if (string.IsNullOrWhiteSpace(typeName) || !typeNameToId.TryGetValue(typeName.Trim(), out var listManagementId))
                {
                    result.RowErrors.Add(new ExcelImportRowError { RowNumber = rowNum, Error = "Type must match a customer type name from List Management.", Value = typeName ?? "(empty)" });
                    result.ErrorCount++;
                    rowNum++;
                    continue;
                }

                decimal? creditLimit = null;
                if (colCreditLimit > 0)
                {
                    var cl = GetCellString(row, colCreditLimit);
                    if (!string.IsNullOrWhiteSpace(cl) && decimal.TryParse(cl, NumberStyles.Any, CultureInfo.InvariantCulture, out var clVal))
                        creditLimit = clVal;
                }
                decimal? percentage = null;
                if (colPercentage > 0)
                {
                    var pct = GetCellString(row, colPercentage);
                    if (!string.IsNullOrWhiteSpace(pct) && decimal.TryParse(pct, NumberStyles.Any, CultureInfo.InvariantCulture, out var pctVal))
                        percentage = pctVal;
                }
                decimal? customPrice = null;
                if (colCustomPrice > 0)
                {
                    var cp = GetCellString(row, colCustomPrice);
                    if (!string.IsNullOrWhiteSpace(cp) && decimal.TryParse(cp, NumberStyles.Any, CultureInfo.InvariantCulture, out var cpVal))
                        customPrice = cpVal;
                }
                bool isActive = true;
                if (colIsActive > 0)
                {
                    var val = GetCellString(row, colIsActive);
                    isActive = string.IsNullOrWhiteSpace(val) || string.Equals(val, "1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(val, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(val, "yes", StringComparison.OrdinalIgnoreCase);
                }

                var customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    Name = name.Trim(),
                    Phone = colPhone > 0 ? GetCellString(row, colPhone) ?? string.Empty : string.Empty,
                    Address = colAddress > 0 ? GetCellString(row, colAddress) : null,
                    City = colCity > 0 ? GetCellString(row, colCity) : null,
                    VehicleNumber = colVehicleNumber > 0 ? GetCellString(row, colVehicleNumber) : null,
                    CreditLimit = creditLimit,
                    IsActive = isActive,
                    Percentage = percentage,
                    CustomPrice = customPrice,
                    ListManagementId = listManagementId
                };
                customersToAdd.Add(customer);
                result.ImportedCount++;
                rowNum++;
            }

            // Only persist to the database if ALL rows are valid
            if (customersToAdd.Count > 0 && result.ErrorCount == 0)
            {
                await _context.Customer.AddRangeAsync(customersToAdd, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else if (result.ErrorCount > 0)
            {
                // We validated some rows successfully but are treating the import as atomic.
                // Since we are not saving anything, reflect that no rows were actually imported.
                result.ImportedCount = 0;
            }
        }

        private static string? GetCellString(IXLRow row, int col)
        {
            if (col <= 0) return null;
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return null;
            return cell.GetString()?.Trim();
        }
    }
}
