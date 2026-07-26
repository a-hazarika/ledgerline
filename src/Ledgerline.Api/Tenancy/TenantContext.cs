namespace Ledgerline.Api.Tenancy;

/// <summary>
/// The tenant the current unit of work belongs to. Scoped: one per request, or one
/// per scope created by a background service.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string Slug { get; }
    bool IsResolved { get; }
}

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string Slug { get; private set; } = "";
    public bool IsResolved => TenantId != Guid.Empty;

    public void Bind(Guid tenantId, string slug)
    {
        TenantId = tenantId;
        Slug = slug;
    }
}
