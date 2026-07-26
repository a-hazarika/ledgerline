using Ledgerline.Api.Data;
using Ledgerline.Api.Email;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Features.Emails;

public sealed record EmailActivityDto(
    Guid Id,
    Guid InvoiceId,
    string InvoiceNumber,
    string ToAddress,
    string Subject,
    string Status,
    string? Error,
    DateTimeOffset QueuedAt,
    DateTimeOffset? SentAt);

public static class EmailActivityEndpoints
{
    public static IEndpointRouteBuilder MapEmailActivityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/emails").WithTags("Email");

        group.MapGet("/", async (int? take, LedgerlineDbContext db, CancellationToken ct) =>
            Results.Ok(await db.EmailLog
                .AsNoTracking()
                .OrderByDescending(e => e.QueuedAt)
                .Take(Math.Clamp(take ?? 50, 1, 200))
                .Join(db.Invoices, e => e.InvoiceId, i => i.Id, (e, i) => new EmailActivityDto(
                    e.Id, e.InvoiceId, i.Number, e.ToAddress, e.Subject, e.Status, e.Error, e.QueuedAt, e.SentAt))
                .ToListAsync(ct)));

        group.MapGet("/queue", (IEmailQueue queue) => Results.Ok(new { depth = queue.Depth }));

        return routes;
    }
}
