using Ledgerline.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Features.Admin;

public sealed record TenantRollup(
    Guid TenantId,
    string Slug,
    string Name,
    string Plan,
    int InvoiceCount,
    long BilledCents,
    long OpenCents);

public sealed record PlatformReport(
    int TenantCount,
    long BilledCents,
    long OpenCents,
    IReadOnlyList<TenantRollup> Tenants);

/// <summary>
/// Platform-wide billing rollup for the operator console. This runs outside any
/// tenant scope: it is meant to see every tenant at once.
/// </summary>
public sealed class PlatformReportService
{
    private readonly LedgerlineDbContext _db;

    public PlatformReportService(LedgerlineDbContext db) => _db = db;

    public async Task<PlatformReport> BuildAsync(CancellationToken cancellationToken)
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var rows = await _db.Invoices
            .AsNoTracking()
            .Select(i => new { i.TenantId, i.Status, i.TotalCents })
            .ToListAsync(cancellationToken);

        var byTenant = rows
            .GroupBy(r => r.TenantId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Count: g.Count(),
                    Billed: g.Sum(r => r.TotalCents),
                    Open: g.Where(r => r.Status == InvoiceStatus.Sent).Sum(r => r.TotalCents)));

        var rollups = tenants
            .Select(t =>
            {
                byTenant.TryGetValue(t.Id, out var totals);
                return new TenantRollup(t.Id, t.Slug, t.Name, t.Plan, totals.Count, totals.Billed, totals.Open);
            })
            .ToArray();

        return new PlatformReport(
            rollups.Length,
            rollups.Sum(r => r.BilledCents),
            rollups.Sum(r => r.OpenCents),
            rollups);
    }
}
