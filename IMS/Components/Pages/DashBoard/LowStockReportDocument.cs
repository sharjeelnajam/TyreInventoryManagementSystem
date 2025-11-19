using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using System.Collections.Generic;

public class LowStockRow
{
    public int SrNo { get; set; }
    public string Product { get; set; }
    public string Type { get; set; }
    public int Stock { get; set; }
}

public class LowStockReportDocument : IDocument
{
    public List<LowStockRow> Rows { get; }

    public LowStockReportDocument(List<LowStockRow> rows)
    {
        Rows = rows;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(25);

            page.Header()
                .Text("Low Stock Products Report")
                .Bold()
                .FontSize(20)
                .AlignCenter();

            page.Content().PaddingTop(50).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(50);
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.ConstantColumn(60);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Text("Sr#").Bold();
                    header.Cell().Text("Product").Bold();
                    header.Cell().Text("Type").Bold();
                    header.Cell().Text("Stock").Bold();
                });

                // Rows
                foreach (var row in Rows)
                {
                    table.Cell().Text(row.SrNo.ToString());
                    table.Cell().Text(row.Product);
                    table.Cell().Text(row.Type);
                    table.Cell().Text(row.Stock.ToString());
                }
            });

            page.Footer()
                .AlignCenter()
                .Text(txt =>
                {
                    txt.Span("Generated on: ").SemiBold();
                    txt.Span(System.DateTime.Now.ToString("dd MMM yyyy"));
                });
        });
    }
}
