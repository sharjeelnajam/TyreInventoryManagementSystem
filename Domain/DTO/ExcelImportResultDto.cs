namespace Domain.DTO
{
    public class ExcelImportResultDto
    {
        public bool Success { get; set; }
        public int TotalRows { get; set; }
        public int ImportedCount { get; set; }
        public int ErrorCount { get; set; }
        public string? Message { get; set; }
        public List<ExcelImportRowError> RowErrors { get; set; } = new();
    }

    public class ExcelImportRowError
    {
        public int RowNumber { get; set; }
        public string? Value { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
