using Ledgerline.Api.Data;
using Ledgerline.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Features.Customers;

public sealed record CustomerDto(Guid Id, string Name, string Email, string? ExternalRef, int InvoiceCount);

public sealed record CreateCustomerRequest(string Name, string Email, string? ExternalRef);

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers").WithTags("Customers");

        group.MapGet("/", async (LedgerlineDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Customers
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CustomerDto(
                    c.Id, c.Name, c.Email, c.ExternalRef,
                    db.Invoices.Count(i => i.CustomerId == c.Id)))
                .ToListAsync(ct)));

        group.MapPost("/", async (
            CreateCustomerRequest request,
            LedgerlineDbContext db,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["Name and email are required."]
                });
            }

            var customer = new Customer
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.TenantId,
                Name = request.Name.Trim(),
                Email = request.Email.Trim(),
                ExternalRef = request.ExternalRef,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/api/customers/{customer.Id}",
                new CustomerDto(customer.Id, customer.Name, customer.Email, customer.ExternalRef, 0));
        });

        return routes;
    }
}
