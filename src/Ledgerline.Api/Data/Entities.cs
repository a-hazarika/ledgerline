namespace Ledgerline.Api.Data;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Plan { get; set; } = "standard";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TenantSettings
{
    public Guid TenantId { get; set; }
    public string LegalName { get; set; } = "";
    public string AccentColor { get; set; } = "#2f6f4e";
    public string LogoFile { get; set; } = "default.svg";
    public string ReplyTo { get; set; } = "";
    public string RemitTo { get; set; } = "";
    public string EmailFooter { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Customer
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? ExternalRef { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class InvoiceStatus
{
    public const string Draft = "draft";
    public const string Sent = "sent";
    public const string Paid = "paid";
    public const string Void = "void";

    public static readonly string[] All = [Draft, Sent, Paid, Void];

    public static bool IsOpen(string status) => status is Sent or Draft;
}

public sealed class Invoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string Number { get; set; } = "";
    public string Status { get; set; } = InvoiceStatus.Draft;
    public string Currency { get; set; } = "USD";
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public long SubtotalCents { get; set; }
    public long TaxCents { get; set; }
    public long TotalCents { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];
}

public sealed class InvoiceLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public int Position { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public long UnitPriceCents { get; set; }

    /// <summary>Tax rate in basis points; 875 == 8.75%.</summary>
    public int TaxRateBp { get; set; }
}

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public long AmountCents { get; set; }
    public string Method { get; set; } = "ach";
    public string? Reference { get; set; }
    public DateTimeOffset PaidAt { get; set; }
}

public sealed class EmailLogEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public string ToAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Status { get; set; } = "queued";
    public string? Error { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
