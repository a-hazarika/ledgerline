using Ledgerline.Api.Data;
using Ledgerline.Api.Domain;
using Xunit;

namespace Ledgerline.Api.Tests;

public class InvoiceCalculatorTests
{
    private static InvoiceLine Line(decimal quantity, long unitPriceCents, int taxRateBp = 0) => new()
    {
        Quantity = quantity,
        UnitPriceCents = unitPriceCents,
        TaxRateBp = taxRateBp
    };

    [Fact]
    public void Sums_line_amounts_into_the_subtotal()
    {
        var totals = InvoiceCalculator.Compute([Line(2, 25_000), Line(1, 7_500)]);

        Assert.Equal(57_500, totals.SubtotalCents);
        Assert.Equal(0, totals.TaxCents);
        Assert.Equal(57_500, totals.TotalCents);
    }

    [Fact]
    public void Applies_tax_per_line_at_basis_point_precision()
    {
        var totals = InvoiceCalculator.Compute([Line(1, 10_000, taxRateBp: 875)]);

        Assert.Equal(10_000, totals.SubtotalCents);
        Assert.Equal(875, totals.TaxCents);
        Assert.Equal(10_875, totals.TotalCents);
    }

    [Fact]
    public void Rounds_fractional_quantities_to_the_cent_before_taxing()
    {
        // 1.5 x 3333 = 4999.5 -> 5000, taxed at 8.75% -> 437.5 -> 438
        var totals = InvoiceCalculator.Compute([Line(1.5m, 3_333, taxRateBp: 875)]);

        Assert.Equal(5_000, totals.SubtotalCents);
        Assert.Equal(438, totals.TaxCents);
        Assert.Equal(5_438, totals.TotalCents);
    }

    [Fact]
    public void Empty_invoice_totals_zero()
    {
        var totals = InvoiceCalculator.Compute([]);

        Assert.Equal(new InvoiceTotals(0, 0, 0), totals);
    }

    [Theory]
    [InlineData(0, "USD", "$0.00")]
    [InlineData(5, "USD", "$0.05")]
    [InlineData(123_456, "USD", "$1,234.56")]
    [InlineData(-2_500, "EUR", "-€25.00")]
    [InlineData(100, "SEK", "SEK 1.00")]
    public void Formats_money_for_display(long cents, string currency, string expected)
    {
        Assert.Equal(expected, Money.Format(cents, currency));
    }
}
