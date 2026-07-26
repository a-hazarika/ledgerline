namespace Ledgerline.Api.Tenancy;

public sealed class TenantResolutionMiddleware
{
    public const string HeaderName = "X-Tenant";

    private static readonly string[] ExemptPrefixes =
    [
        "/health",
        "/openapi",
        "/branding",
        "/api/admin"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantDirectory directory, TenantContext tenantContext)
    {
        if (IsExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var slug = context.Request.Headers[HeaderName].ToString().Trim();
        if (string.IsNullOrEmpty(slug))
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                $"Missing {HeaderName} header.");
            return;
        }

        var tenant = await directory.FindBySlugAsync(slug, context.RequestAborted);
        if (tenant is null)
        {
            _logger.LogWarning("Rejected request for unknown tenant slug {Slug}", slug);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                $"Unknown tenant '{slug}'.");
            return;
        }

        tenantContext.Bind(tenant.Value.Id, tenant.Value.Slug);
        await _next(context);
    }

    private static bool IsExempt(PathString path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = status == StatusCodes.Status404NotFound ? "Tenant not found" : "Tenant required",
            status,
            detail
        });
    }
}
