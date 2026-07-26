using Ledgerline.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ledgerline.Api.Tests;

public class TenantScopingTests
{
    [Fact]
    public async Task A_bound_session_only_sees_its_own_tenants_invoices()
    {
        var name = TestDb.NewDatabaseName();
        Guid northwind, atlas;

        await using (var seed = TestDb.Open(name))
        {
            northwind = seed.AddTenant("northwind-studio", "Northwind Studio").Id;
            atlas = seed.AddTenant("atlas-freight", "Atlas Freight").Id;

            seed.AddInvoice(northwind, "INV-01001", InvoiceStatus.Sent, 120_00);
            seed.AddInvoice(northwind, "INV-01002", InvoiceStatus.Draft, 40_00);
            seed.AddInvoice(atlas, "INV-01001", InvoiceStatus.Sent, 900_00);

            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var db = TestDb.Open(name, northwind);
        var numbers = await db.Invoices.Select(i => i.Number).OrderBy(n => n).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["INV-01001", "INV-01002"], numbers);
        var all = await db.Invoices.ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(all, i => Assert.Equal(northwind, i.TenantId));
    }

    [Fact]
    public async Task Numbers_are_only_unique_within_a_tenant()
    {
        var name = TestDb.NewDatabaseName();

        await using var seed = TestDb.Open(name);
        var northwind = seed.AddTenant("northwind-studio", "Northwind Studio").Id;
        var atlas = seed.AddTenant("atlas-freight", "Atlas Freight").Id;

        seed.AddInvoice(northwind, "INV-01001", InvoiceStatus.Sent, 120_00);
        seed.AddInvoice(atlas, "INV-01001", InvoiceStatus.Draft, 900_00);
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var scoped = TestDb.Open(name, atlas);
        var invoice = await scoped.Invoices.SingleAsync(i => i.Number == "INV-01001", TestContext.Current.CancellationToken);

        Assert.Equal(atlas, invoice.TenantId);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }
}
