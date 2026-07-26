using Ledgerline.Api.Data;
using Ledgerline.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Features.Settings;

public sealed record BrandingDto(
    string LegalName,
    string AccentColor,
    string LogoFile,
    string ReplyTo,
    string RemitTo,
    string EmailFooter);

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/", async (LedgerlineDbContext db, CancellationToken ct) =>
        {
            var settings = await db.TenantSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            return settings is null
                ? Results.NotFound()
                : Results.Ok(new BrandingDto(
                    settings.LegalName, settings.AccentColor, settings.LogoFile,
                    settings.ReplyTo, settings.RemitTo, settings.EmailFooter));
        });

        group.MapPut("/", async (
            BrandingDto request,
            LedgerlineDbContext db,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var settings = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenant.TenantId, ct);
            if (settings is null)
            {
                return Results.NotFound();
            }

            settings.LegalName = request.LegalName;
            settings.AccentColor = request.AccentColor;
            settings.LogoFile = request.LogoFile;
            settings.ReplyTo = request.ReplyTo;
            settings.RemitTo = request.RemitTo;
            settings.EmailFooter = request.EmailFooter;
            settings.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(request);
        });

        return routes;
    }
}
