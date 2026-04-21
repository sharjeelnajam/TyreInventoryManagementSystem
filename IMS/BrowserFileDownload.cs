using System.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace IMS;

/// <summary>
/// PDF helpers: small files can use stream interop; receipts/invoices use same-origin fetch + blob
/// so downloads stay reliable on Blazor Server (no large SignalR payloads).
/// </summary>
public static class BrowserFileDownload
{
    /// <summary>Uses <c>fetch</c> + blob (auth cookies). Prefer for thermal receipts and full invoices.</summary>
    public static Task DownloadPdfFromAuthenticatedUrlAsync(IJSRuntime js, NavigationManager nav, string relativeUrl, string fileName)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(nav);
        var url = nav.ToAbsoluteUri(relativeUrl).ToString();
        return DownloadViaAuthenticatedUrlWithFallbackAsync(js, fileName, url);
    }

    private static async Task DownloadViaAuthenticatedUrlWithFallbackAsync(IJSRuntime js, string fileName, string url)
    {
        try
        {
            await js.InvokeVoidAsync("downloadFileFromAuthenticatedUrl", fileName, url);
        }
        catch (JSException ex) when (ex.Message.Contains("downloadFileFromAuthenticatedUrl", StringComparison.Ordinal))
        {
            // Fallback for stale cached site.js: use browser default download behavior.
            await js.InvokeVoidAsync("open", url, "_blank");
        }
    }

    public static async Task DownloadPdfAsync(IJSRuntime js, string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(js);
        if (content.Length == 0)
            return;

        var stream = new MemoryStream(content, writable: false);
        using var streamRef = new DotNetStreamReference(stream);
        await js.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
    }
}
