using Ledgerline.Api.Data;
using Ledgerline.Api.Features.Admin;
using Xunit;

namespace Ledgerline.Api.Tests;

/// <summary>
/// The operator console is not a tenant, so these run on an unbound session.
/// </summary>
public class PlatformReportServiceTests
{
    [Fact]
    public async Task Rolls_up_every_tenant_on_the_platform()
    {
        var name = TestDb.NewDatabaseName();

        await using (var seed = TestDb.Open(name))
        {
            var northwind = seed.AddTenant("northwind-studio", "Northwind Studio").Id;
            var atlas = seed.AddTenant("atlas-freight", "Atlas Freight", plan: "growth").Id;

            seed.AddInvoice(northwind, "INV-01001", InvoiceStatus.Sent, 120_00);
            seed.AddInvoice(northwind, "INV-01002", InvoiceStatus.Paid, 80_00);
            seed.AddInvoice(atlas, "INV-01001", InvoiceStatus.Sent, 900_00);

            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var db = TestDb.Open(name);
        var report = await new PlatformReportService(db).BuildAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, report.TenantCount);
        Assert.Equal(1100_00, report.BilledCents);
        Assert.Equal(1020_00, report.OpenCents);

        var atlasRollup = Assert.Single(report.Tenants, t => t.Slug == "atlas-freight");
        Assert.Equal("growth", atlasRollup.Plan);
        Assert.Equal(1, atlasRollup.InvoiceCount);
        Assert.Equal(900_00, atlasRollup.BilledCents);
    }

    [Fact]
    public async Task Lists_tenants_that_have_not_invoiced_yet()
    {
        var name = TestDb.NewDatabaseName();

        await using (var seed = TestDb.Open(name))
        {
            seed.AddTenant("verity-health", "Verity Health", plan: "enterprise");
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var db = TestDb.Open(name);
        var report = await new PlatformReportService(db).BuildAsync(TestContext.Current.CancellationToken);

        var rollup = Assert.Single(report.Tenants);
        Assert.Equal(0, rollup.InvoiceCount);
        Assert.Equal(0, rollup.BilledCents);
    }
}
