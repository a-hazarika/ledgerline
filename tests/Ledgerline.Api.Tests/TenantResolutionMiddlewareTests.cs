using Ledgerline.Api.Data;
using Ledgerline.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledgerline.Api.Tests;

public class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task Binds_the_tenant_named_by_the_header()
    {
        await using var provider = await BuildProviderAsync();
        using var scope = provider.CreateScope();

        var context = NewRequest(scope, "/api/invoices", tenantSlug: "northwind-studio");
        var reachedEndpoint = await InvokeAsync(scope, context);

        Assert.True(reachedEndpoint);

        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        Assert.True(tenant.IsResolved);
        Assert.Equal("northwind-studio", tenant.Slug);
    }

    [Fact]
    public async Task Rejects_a_request_with_no_tenant_header()
    {
        await using var provider = await BuildProviderAsync();
        using var scope = provider.CreateScope();

        var context = NewRequest(scope, "/api/invoices", tenantSlug: null);
        var reachedEndpoint = await InvokeAsync(scope, context);

        Assert.False(reachedEndpoint);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_an_unknown_tenant_slug()
    {
        await using var provider = await BuildProviderAsync();
        using var scope = provider.CreateScope();

        var context = NewRequest(scope, "/api/invoices", tenantSlug: "does-not-exist");
        var reachedEndpoint = await InvokeAsync(scope, context);

        Assert.False(reachedEndpoint);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Leaves_operator_endpoints_unbound()
    {
        await using var provider = await BuildProviderAsync();
        using var scope = provider.CreateScope();

        var context = NewRequest(scope, "/api/admin/report", tenantSlug: null);
        var reachedEndpoint = await InvokeAsync(scope, context);

        Assert.True(reachedEndpoint);
        Assert.False(scope.ServiceProvider.GetRequiredService<ITenantContext>().IsResolved);
    }

    private static async Task<ServiceProvider> BuildProviderAsync()
    {
        var databaseName = TestDb.NewDatabaseName();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<LedgerlineDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TenantDirectory>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LedgerlineDbContext>();
            db.AddTenant("northwind-studio", "Northwind Studio");
            db.AddTenant("atlas-freight", "Atlas Freight");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return provider;
    }

    private static DefaultHttpContext NewRequest(IServiceScope scope, string path, string? tenantSlug)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };

        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (tenantSlug is not null)
        {
            context.Request.Headers[TenantResolutionMiddleware.HeaderName] = tenantSlug;
        }

        return context;
    }

    private static async Task<bool> InvokeAsync(IServiceScope scope, HttpContext context)
    {
        var reached = false;

        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                reached = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            scope.ServiceProvider.GetRequiredService<TenantDirectory>(),
            scope.ServiceProvider.GetRequiredService<TenantContext>());

        return reached;
    }
}
