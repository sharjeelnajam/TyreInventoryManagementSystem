namespace Domain;

/// <summary>How a sale line is described when the catalog name differs from what the customer sees on paperwork.</summary>
public static class SaleDetailLineDisplay
{
    /// <summary>Matches A4 invoice PDF: product name unless a line override was stored.</summary>
    public static string InvoiceDescription(SaleDetail d)
    {
        if (!string.IsNullOrWhiteSpace(d.LineDisplayName))
            return d.LineDisplayName.Trim();

        return d.Product?.ProductName ?? "N/A";
    }

    /// <summary>Thermal / receipt style: catalog line includes brand; override replaces both.</summary>
    public static string ReceiptDescription(SaleDetail d)
    {
        if (!string.IsNullOrWhiteSpace(d.LineDisplayName))
            return d.LineDisplayName.Trim();

        var name = d.Product?.ProductName ?? "Item";
        var brand = d.Brand ?? d.Product?.Brand;
        return string.IsNullOrWhiteSpace(brand) ? name : $"{name} ({brand})";
    }
}
