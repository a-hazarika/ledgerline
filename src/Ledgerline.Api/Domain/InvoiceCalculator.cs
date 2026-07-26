using Ledgerline.Api.Data;

namespace Ledgerline.Api.Domain;

public readonly record struct InvoiceTotals(long SubtotalCents, long TaxCents, long TotalCents);

public static class InvoiceCalculator
{
    /// <summary>
    /// Line amounts are rounded to the cent before tax is applied, matching the way
    /// the numbers are presented on the invoice itself.
    /// </summary>
    public static InvoiceTotals Compute(IEnumerable<InvoiceLine> lines)
    {
        long subtotal = 0;
        long tax = 0;

        foreach (var line in lines)
        {
            var amount = RoundToCents(line.Quantity * line.UnitPriceCents);
            subtotal += amount;
            tax += RoundToCents(amount * line.TaxRateBp / 10_000m);
        }

        return new InvoiceTotals(subtotal, tax, subtotal + tax);
    }

    public static long LineAmountCents(InvoiceLine line) =>
        RoundToCents(line.Quantity * line.UnitPriceCents);

    private static long RoundToCents(decimal value) =>
        (long)Math.Round(value, MidpointRounding.AwayFromZero);
}

public static class Money
{
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "$",
        ["EUR"] = "€",
        ["GBP"] = "£"
    };

    public static string Format(long cents, string currency)
    {
        var symbol = Symbols.TryGetValue(currency, out var s) ? s : currency + " ";
        var sign = cents < 0 ? "-" : "";
        var abs = Math.Abs(cents);
        return $"{sign}{symbol}{abs / 100:N0}.{abs % 100:00}";
    }
}
