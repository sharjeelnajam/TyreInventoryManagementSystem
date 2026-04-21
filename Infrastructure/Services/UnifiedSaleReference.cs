using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    /// <summary>
    /// One incrementing 5-digit reference (00001, 00002, …) shared by Shop Billing bills and POS / manual sales.
    /// Sequence is computed across all tenants so numbers never repeat in the database (multi-branch UI lists every bill).
    /// Legacy values like "SO-2026…" or "SB-…" are ignored when computing the max.
    /// </summary>
    public static class UnifiedSaleReference
    {
        public static async Task<int> GetMaxSequenceAsync(ApplicationDbContext context, Guid tenantId)
        {
            _ = tenantId;

            var billNums = await context.ShopServiceBills.Select(b => b.BillNumber).ToListAsync();
            var saleNums = await context.Sale.Select(s => s.SaleNumber).ToListAsync();

            var max = 0;
            foreach (var n in billNums)
            {
                if (TryParseUnifiedNumber(n, out var v) && v > max)
                    max = v;
            }
            foreach (var n in saleNums)
            {
                if (TryParseUnifiedNumber(n, out var v) && v > max)
                    max = v;
            }
            return max;
        }

        public static string FormatSequence(int sequence) =>
            sequence.ToString("D5", CultureInfo.InvariantCulture);

        /// <summary>Allocates the next reference string from current DB state (caller should run inside a transaction for concurrency).</summary>
        public static async Task<string> AllocateNextAsync(ApplicationDbContext context, Guid tenantId)
        {
            var max = await GetMaxSequenceAsync(context, tenantId);
            return FormatSequence(max + 1);
        }

        /// <summary>Only strings that are entirely digits (e.g. 00003) count.</summary>
        public static bool TryParseUnifiedNumber(string? s, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s))
                return false;
            s = s.Trim();
            if (s.Length == 0)
                return false;
            foreach (var c in s)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }
}
