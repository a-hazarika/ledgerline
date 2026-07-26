using Ledgerline.Api.Data;

namespace Ledgerline.Api.Email;

/// <summary>Everything the invoice template needs about the sending business.</summary>
public sealed record BrandProfile(
    string LegalName,
    string AccentColor,
    string LogoFile,
    string ReplyTo,
    string RemitTo,
    string Footer)
{
    public static readonly BrandProfile Fallback = new(
        "Ledgerline", "#333333", "default.svg", "no-reply@ledgerline.test", "", "");

    public static BrandProfile From(TenantSettings settings) => new(
        settings.LegalName,
        settings.AccentColor,
        settings.LogoFile,
        settings.ReplyTo,
        settings.RemitTo,
        settings.EmailFooter);
}

public sealed record InvoiceEmailJob(Guid TenantId, Guid InvoiceId, Guid EmailLogId);

public sealed record InvoiceLineView(
    int Position,
    string Description,
    decimal Quantity,
    long UnitPriceCents,
    long AmountCents);

public sealed record InvoiceView(
    Guid Id,
    string Number,
    string Currency,
    DateOnly IssuedOn,
    DateOnly DueOn,
    long SubtotalCents,
    long TaxCents,
    long TotalCents,
    string CustomerName,
    string CustomerEmail,
    IReadOnlyList<InvoiceLineView> Lines);

/// <summary>Balance the recipient still owes, excluding the invoice being sent.</summary>
public sealed record AccountSummary(int OpenInvoiceCount, long OpenBalanceCents);

public sealed record RenderedEmail(
    string FromName,
    string FromAddress,
    string ReplyTo,
    string Subject,
    string HtmlBody);

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 1025;
    public string FromAddress { get; set; } = "invoices@ledgerline.test";
    public int SenderConcurrency { get; set; } = 4;
}
