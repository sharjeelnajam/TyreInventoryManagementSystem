using Domain.Enums;

namespace Domain.DTO
{
    public sealed class VatBreakdown
    {
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal NetAmount { get; set; }
        public VatMode Mode { get; set; }
    }

    public static class VatCalculator
    {
        private const decimal VatRate = 0.20m;

        public static VatBreakdown Calculate(decimal subtotal, decimal discount, VatMode mode)
        {
            var safeSubtotal = Math.Max(subtotal, 0m);
            var safeDiscount = Math.Clamp(discount, 0m, safeSubtotal);
            var discountedAmount = Math.Max(safeSubtotal - safeDiscount, 0m);

            decimal vatAmount;
            decimal netAmount;

            switch (mode)
            {
                case VatMode.PlusVat:
                    vatAmount = Math.Round(discountedAmount * VatRate, 2);
                    netAmount = Math.Round(discountedAmount + vatAmount, 2);
                    break;
                case VatMode.IncludingVat:
                    // Per requested behavior: VAT is 20% of entered/discounted total.
                    vatAmount = Math.Round(discountedAmount * VatRate, 2);
                    netAmount = Math.Round(discountedAmount, 2);
                    break;
                default:
                    vatAmount = 0m;
                    netAmount = Math.Round(discountedAmount, 2);
                    break;
            }

            return new VatBreakdown
            {
                Subtotal = Math.Round(safeSubtotal, 2),
                Discount = Math.Round(safeDiscount, 2),
                VatAmount = vatAmount,
                NetAmount = netAmount,
                Mode = mode
            };
        }
    }
}
