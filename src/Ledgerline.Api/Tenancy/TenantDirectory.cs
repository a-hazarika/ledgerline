using Ledgerline.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Tenancy;

public readonly record struct TenantRef(Guid Id, string Slug, string Name);

/// <summary>
/// Slug -> tenant lookup. Every request hits this, and the tenant list changes only
/// when someone signs up, so it is cached process-wide behind a short TTL.
/// </summary>
public sealed class TenantDirectory
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    private IReadOnlyDictionary<string, TenantRef> _bySlug =
        new Dictionary<string, TenantRef>(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    public TenantDirectory(IServiceScopeFactory scopes) => _scopes = scopes;

    public async ValueTask<TenantRef?> FindBySlugAsync(string slug, CancellationToken ct)
    {
        var snapshot = _bySlug;
        if (DateTimeOffset.UtcNow - _loadedAt > Ttl)
        {
            snapshot = await ReloadAsync(ct);
        }

        return snapshot.TryGetValue(slug, out var tenant) ? tenant : null;
    }

    public async ValueTask<IReadOnlyCollection<TenantRef>> ListAsync(CancellationToken ct)
    {
        var snapshot = _bySlug;
        if (DateTimeOffset.UtcNow - _loadedAt > Ttl)
        {
            snapshot = await ReloadAsync(ct);
        }

        return snapshot.Values.OrderBy(t => t.Name).ToArray();
    }

    private async Task<IReadOnlyDictionary<string, TenantRef>> ReloadAsync(CancellationToken ct)
    {
        await _reloadGate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - _loadedAt <= Ttl)
            {
                return _bySlug;
            }

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LedgerlineDbContext>();

            var loaded = await db.Tenants
                .AsNoTracking()
                .Select(t => new TenantRef(t.Id, t.Slug, t.Name))
                .ToDictionaryAsync(t => t.Slug, StringComparer.OrdinalIgnoreCase, ct);

            // Publish the new map in one assignment so readers never observe a partial view.
            _bySlug = loaded;
            _loadedAt = DateTimeOffset.UtcNow;
            return loaded;
        }
        finally
        {
            _reloadGate.Release();
        }
    }
}
