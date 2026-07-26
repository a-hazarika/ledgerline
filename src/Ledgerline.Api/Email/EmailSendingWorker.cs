using Ledgerline.Api.Data;
using Ledgerline.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ledgerline.Api.Email;

/// <summary>
/// Drains the outbound queue. Sending is network-bound, so several jobs are in flight
/// at once; each one gets its own service scope and its own database session.
/// </summary>
public sealed class EmailSendingWorker : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IInvoiceRenderer _renderer;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailSendingWorker> _logger;

    public EmailSendingWorker(
        IEmailQueue queue,
        IServiceScopeFactory scopes,
        IInvoiceRenderer renderer,
        IEmailSender sender,
        IOptions<EmailOptions> options,
        ILogger<EmailSendingWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _renderer = renderer;
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbound email worker started ({Concurrency} concurrent senders)",
            _options.SenderConcurrency);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _options.SenderConcurrency),
            CancellationToken = stoppingToken
        };

        try
        {
            await Parallel.ForEachAsync(_queue.ReadAllAsync(stoppingToken), parallelOptions, ProcessAsync);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Outbound email worker stopping; {Pending} job(s) left in queue", _queue.Depth);
        }
    }

    private async ValueTask ProcessAsync(InvoiceEmailJob job, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerlineDbContext>();

        try
        {
            await DeliverAsync(db, job, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to send invoice {InvoiceId}", job.InvoiceId);
            await MarkFailedAsync(db, job, ex.Message, cancellationToken);
        }
    }

    private async Task DeliverAsync(LedgerlineDbContext db, InvoiceEmailJob job, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == job.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice {InvoiceId} vanished before it could be sent", job.InvoiceId);
            return;
        }

        var customer = await db.Customers.FirstAsync(c => c.Id == invoice.CustomerId, cancellationToken);
        var settings = await db.TenantSettings.FirstAsync(s => s.TenantId == job.TenantId, cancellationToken);
        var summary = await BuildAccountSummaryAsync(db, invoice, cancellationToken);

        _renderer.ApplyBranding(BrandProfile.From(settings));
        var message = await _renderer.RenderInvoiceAsync(BuildView(invoice, customer), summary, cancellationToken);

        await _sender.SendAsync(message, customer.Email, customer.Name, cancellationToken);

        invoice.Status = InvoiceStatus.Sent;
        invoice.SentAt = DateTimeOffset.UtcNow;

        var log = await db.EmailLog.FirstOrDefaultAsync(e => e.Id == job.EmailLogId, cancellationToken);
        if (log is not null)
        {
            log.Status = "sent";
            log.Subject = message.Subject;
            log.SentAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await VoidSupersededDraftsAsync(db, invoice, cancellationToken);
    }

    /// <summary>
    /// An invoice number is final once it has left the building, so any draft still
    /// sitting on that number (a revision that was never issued) is dead paper.
    /// </summary>
    private async Task VoidSupersededDraftsAsync(
        LedgerlineDbContext db,
        Invoice sent,
        CancellationToken cancellationToken)
    {
        var voided = await db.Invoices
            .Where(i => i.Number == sent.Number
                        && i.Id != sent.Id
                        && i.Status == InvoiceStatus.Draft)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Status, InvoiceStatus.Void),
                cancellationToken);

        if (voided > 0)
        {
            _logger.LogInformation("Voided {Count} superseded draft(s) of {Number}", voided, sent.Number);
        }
    }

    private static async Task<AccountSummary> BuildAccountSummaryAsync(
        LedgerlineDbContext db,
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var open = await db.Invoices
            .Where(i => i.CustomerId == invoice.CustomerId
                        && i.Id != invoice.Id
                        && i.Status == InvoiceStatus.Sent)
            .Select(i => i.TotalCents)
            .ToListAsync(cancellationToken);

        return new AccountSummary(open.Count, open.Sum());
    }

    private static InvoiceView BuildView(Invoice invoice, Customer customer) => new(
        invoice.Id,
        invoice.Number,
        invoice.Currency,
        invoice.IssuedOn,
        invoice.DueOn,
        invoice.SubtotalCents,
        invoice.TaxCents,
        invoice.TotalCents,
        customer.Name,
        customer.Email,
        invoice.Lines
            .OrderBy(l => l.Position)
            .Select(l => new InvoiceLineView(
                l.Position,
                l.Description,
                l.Quantity,
                l.UnitPriceCents,
                InvoiceCalculator.LineAmountCents(l)))
            .ToArray());

    private async Task MarkFailedAsync(
        LedgerlineDbContext db,
        InvoiceEmailJob job,
        string error,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await db.EmailLog.FirstOrDefaultAsync(e => e.Id == job.EmailLogId, cancellationToken);
            if (log is null)
            {
                return;
            }

            log.Status = "failed";
            log.Error = error.Length > 500 ? error[..500] : error;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not record delivery failure for {InvoiceId}", job.InvoiceId);
        }
    }
}
