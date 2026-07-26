using Ledgerline.Api.Data;
using Ledgerline.Api.Domain;
using Ledgerline.Api.Email;
using Ledgerline.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Features.Invoices;

public sealed record InvoiceLineInput(string Description, decimal Quantity, long UnitPriceCents, int TaxRateBp);

public sealed record CreateInvoiceRequest(
    Guid CustomerId,
    DateOnly? IssuedOn,
    int? TermDays,
    string? Currency,
    string? Notes,
    IReadOnlyList<InvoiceLineInput> Lines);

public sealed record RecordPaymentRequest(long AmountCents, string Method, string? Reference);

public sealed record InvoiceLineDto(
    Guid Id,
    int Position,
    string Description,
    decimal Quantity,
    long UnitPriceCents,
    int TaxRateBp,
    long AmountCents);

public sealed record InvoiceSummaryDto(
    Guid Id,
    string Number,
    string Status,
    string Currency,
    Guid CustomerId,
    string CustomerName,
    DateOnly IssuedOn,
    DateOnly DueOn,
    long TotalCents,
    DateTimeOffset? SentAt);

public sealed record InvoiceDetailDto(
    Guid Id,
    string Number,
    string Status,
    string Currency,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateOnly IssuedOn,
    DateOnly DueOn,
    long SubtotalCents,
    long TaxCents,
    long TotalCents,
    long PaidCents,
    string? Notes,
    DateTimeOffset? SentAt,
    IReadOnlyList<InvoiceLineDto> Lines);

public sealed class InvoiceService
{
    private const int DefaultTermDays = 30;

    private readonly LedgerlineDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEmailQueue _queue;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        LedgerlineDbContext db,
        ITenantContext tenant,
        IEmailQueue queue,
        ILogger<InvoiceService> logger)
    {
        _db = db;
        _tenant = tenant;
        _queue = queue;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InvoiceSummaryDto>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        var query = _db.Invoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status);
        }

        return await query
            .OrderByDescending(i => i.IssuedOn)
            .ThenByDescending(i => i.Number)
            .Join(_db.Customers, i => i.CustomerId, c => c.Id, (i, c) => new InvoiceSummaryDto(
                i.Id, i.Number, i.Status, i.Currency, c.Id, c.Name,
                i.IssuedOn, i.DueOn, i.TotalCents, i.SentAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvoiceDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstAsync(c => c.Id == invoice.CustomerId, cancellationToken);

        var paid = await _db.Payments
            .Where(p => p.InvoiceId == invoice.Id)
            .SumAsync(p => (long?)p.AmountCents, cancellationToken) ?? 0;

        return ToDetail(invoice, customer, paid);
    }

    public async Task<InvoiceDetailDto?> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var issuedOn = request.IssuedOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var invoice = new Invoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenant.TenantId,
            CustomerId = customer.Id,
            Number = await NextNumberAsync(cancellationToken),
            Status = InvoiceStatus.Draft,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            IssuedOn = issuedOn,
            DueOn = issuedOn.AddDays(request.TermDays ?? DefaultTermDays),
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var position = 1;
        foreach (var line in request.Lines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                Id = Guid.CreateVersion7(),
                TenantId = _tenant.TenantId,
                InvoiceId = invoice.Id,
                Position = position++,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPriceCents = line.UnitPriceCents,
                TaxRateBp = line.TaxRateBp
            });
        }

        ApplyTotals(invoice);

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDetail(invoice, customer, paidCents: 0);
    }

    /// <summary>
    /// Starts a revision of an existing invoice. The revision keeps the original
    /// number until one of the two is actually issued.
    /// </summary>
    public async Task<InvoiceDetailDto?> DuplicateAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (source is null)
        {
            return null;
        }

        var customer = await _db.Customers.FirstAsync(c => c.Id == source.CustomerId, cancellationToken);

        var copy = new Invoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenant.TenantId,
            CustomerId = source.CustomerId,
            Number = source.Number,
            Status = InvoiceStatus.Draft,
            Currency = source.Currency,
            IssuedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            DueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(DefaultTermDays),
            Notes = source.Notes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var line in source.Lines.OrderBy(l => l.Position))
        {
            copy.Lines.Add(new InvoiceLine
            {
                Id = Guid.CreateVersion7(),
                TenantId = _tenant.TenantId,
                InvoiceId = copy.Id,
                Position = line.Position,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPriceCents = line.UnitPriceCents,
                TaxRateBp = line.TaxRateBp
            });
        }

        ApplyTotals(copy);

        _db.Invoices.Add(copy);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDetail(copy, customer, paidCents: 0);
    }

    public async Task<Guid?> QueueSendAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var customer = await _db.Customers.FirstAsync(c => c.Id == invoice.CustomerId, cancellationToken);

        var entry = new EmailLogEntry
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenant.TenantId,
            InvoiceId = invoice.Id,
            ToAddress = customer.Email,
            Status = "queued",
            QueuedAt = DateTimeOffset.UtcNow
        };

        _db.EmailLog.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        _queue.Enqueue(new InvoiceEmailJob(_tenant.TenantId, invoice.Id, entry.Id));
        _logger.LogInformation("Queued {Number} for delivery to {Recipient}", invoice.Number, customer.Email);

        return entry.Id;
    }

    public async Task<InvoiceDetailDto?> RecordPaymentAsync(
        Guid id,
        RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        _db.Payments.Add(new Payment
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenant.TenantId,
            InvoiceId = invoice.Id,
            AmountCents = request.AmountCents,
            Method = request.Method,
            Reference = request.Reference,
            PaidAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        var paid = await _db.Payments
            .Where(p => p.InvoiceId == invoice.Id)
            .SumAsync(p => (long?)p.AmountCents, cancellationToken) ?? 0;

        if (paid >= invoice.TotalCents && invoice.Status != InvoiceStatus.Paid)
        {
            invoice.Status = InvoiceStatus.Paid;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var customer = await _db.Customers.FirstAsync(c => c.Id == invoice.CustomerId, cancellationToken);
        return ToDetail(invoice, customer, paid);
    }

    private async Task<string> NextNumberAsync(CancellationToken cancellationToken)
    {
        var last = await _db.Invoices
            .OrderByDescending(i => i.Number)
            .Select(i => i.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = 1000;
        if (last is { Length: > 4 } && int.TryParse(last[4..], out var parsed))
        {
            sequence = parsed;
        }

        return $"INV-{sequence + 1:00000}";
    }

    private static void ApplyTotals(Invoice invoice)
    {
        var totals = InvoiceCalculator.Compute(invoice.Lines);
        invoice.SubtotalCents = totals.SubtotalCents;
        invoice.TaxCents = totals.TaxCents;
        invoice.TotalCents = totals.TotalCents;
    }

    private static InvoiceDetailDto ToDetail(Invoice invoice, Customer customer, long paidCents) => new(
        invoice.Id,
        invoice.Number,
        invoice.Status,
        invoice.Currency,
        customer.Id,
        customer.Name,
        customer.Email,
        invoice.IssuedOn,
        invoice.DueOn,
        invoice.SubtotalCents,
        invoice.TaxCents,
        invoice.TotalCents,
        paidCents,
        invoice.Notes,
        invoice.SentAt,
        invoice.Lines
            .OrderBy(l => l.Position)
            .Select(l => new InvoiceLineDto(
                l.Id, l.Position, l.Description, l.Quantity, l.UnitPriceCents, l.TaxRateBp,
                InvoiceCalculator.LineAmountCents(l)))
            .ToArray());
}
