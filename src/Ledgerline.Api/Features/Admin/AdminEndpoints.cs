using Ledgerline.Api.Tenancy;

namespace Ledgerline.Api.Features.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        // Operator-facing. These run without an X-Tenant header on purpose.
        var group = routes.MapGroup("/api/admin").WithTags("Admin");

        group.MapGet("/tenants", async (TenantDirectory directory, CancellationToken ct) =>
            Results.Ok(await directory.ListAsync(ct)));

        group.MapGet("/report", async (PlatformReportService reports, CancellationToken ct) =>
            Results.Ok(await reports.BuildAsync(ct)));

        return routes;
    }
}
