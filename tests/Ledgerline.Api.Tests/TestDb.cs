using Ledgerline.Api.Data;
using Ledgerline.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Tests;

internal sealed class StubTenantContext : ITenantContext
{
    public Guid TenantId { get; init; }
    public string Slug { get; init; } = "";
    public bool IsResolved => TenantId != Guid.Empty;
}

internal static class TestDb
{
    public static string NewDatabaseName() => "ledgerline-" + Guid.NewGuid().ToString("n");

    /// <summary>Opens a session bound to <paramref name="tenantId"/>, or unbound when omitted.</summary>
    public static LedgerlineDbContext Open(string databaseName, Guid tenantId = default)
    {
        var options = new DbContextOptionsBuilder<LedgerlineDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new LedgerlineDbContext(options, new StubTenantContext { TenantId = tenantId });
    }

    public static Tenant AddTenant(this LedgerlineDbContext db, string slug, string name, string plan = "standard")
    {
        var tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Slug = slug,
            Name = name,
            Plan = plan,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Tenants.Add(tenant);
        return tenant;
    }

    public static Invoice AddInvoice(
        this LedgerlineDbContext db,
        Guid tenantId,
        string number,
        string status,
        long totalCents)
    {
        var invoice = new Invoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CustomerId = Guid.CreateVersion7(),
            Number = number,
            Status = status,
            Currency = "USD",
            IssuedOn = new DateOnly(2026, 1, 5),
            DueOn = new DateOnly(2026, 2, 4),
            SubtotalCents = totalCents,
            TotalCents = totalCents,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Invoices.Add(invoice);
        return invoice;
    }
}
