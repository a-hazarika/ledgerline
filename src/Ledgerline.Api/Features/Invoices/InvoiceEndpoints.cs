using Ledgerline.Api.Data;

namespace Ledgerline.Api.Features.Invoices;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/invoices").WithTags("Invoices");

        group.MapGet("/", async (string? status, InvoiceService service, CancellationToken ct) =>
        {
            if (status is not null && !InvoiceStatus.All.Contains(status))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"Unknown status '{status}'."]
                });
            }

            return Results.Ok(await service.ListAsync(status, ct));
        });

        group.MapGet("/{id:guid}", async (Guid id, InvoiceService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } invoice
                ? Results.Ok(invoice)
                : Results.NotFound());

        group.MapPost("/", async (CreateInvoiceRequest request, InvoiceService service, CancellationToken ct) =>
        {
            if (request.Lines is null || request.Lines.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["lines"] = ["An invoice needs at least one line."]
                });
            }

            var created = await service.CreateAsync(request, ct);
            return created is null
                ? Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["customerId"] = ["Unknown customer."]
                })
                : Results.Created($"/api/invoices/{created.Id}", created);
        });

        group.MapPost("/{id:guid}/duplicate", async (Guid id, InvoiceService service, CancellationToken ct) =>
            await service.DuplicateAsync(id, ct) is { } copy
                ? Results.Created($"/api/invoices/{copy.Id}", copy)
                : Results.NotFound());

        group.MapPost("/{id:guid}/send", async (Guid id, InvoiceService service, CancellationToken ct) =>
            await service.QueueSendAsync(id, ct) is { } emailId
                ? Results.Accepted($"/api/invoices/{id}", new { emailLogId = emailId })
                : Results.NotFound());

        group.MapPost("/{id:guid}/payments", async (
            Guid id,
            RecordPaymentRequest request,
            InvoiceService service,
            CancellationToken ct) =>
        {
            if (request.AmountCents <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amountCents"] = ["Payment amount must be positive."]
                });
            }

            return await service.RecordPaymentAsync(id, request, ct) is { } invoice
                ? Results.Ok(invoice)
                : Results.NotFound();
        });

        return routes;
    }
}
