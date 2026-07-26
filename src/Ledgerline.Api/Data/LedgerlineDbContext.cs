using Ledgerline.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ledgerline.Api.Data;

public sealed class LedgerlineDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public LedgerlineDbContext(DbContextOptions<LedgerlineDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<EmailLogEntry> EmailLog => Set<EmailLogEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Slug).HasColumnName("slug");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Plan).HasColumnName("plan");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<TenantSettings>(e =>
        {
            e.ToTable("tenant_settings");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.LegalName).HasColumnName("legal_name");
            e.Property(x => x.AccentColor).HasColumnName("accent_color");
            e.Property(x => x.LogoFile).HasColumnName("logo_file");
            e.Property(x => x.ReplyTo).HasColumnName("reply_to");
            e.Property(x => x.RemitTo).HasColumnName("remit_to");
            e.Property(x => x.EmailFooter).HasColumnName("email_footer");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.ExternalRef).HasColumnName("external_ref");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<Invoice>(e =>
        {
            e.ToTable("invoices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Number).HasColumnName("number");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Currency).HasColumnName("currency");
            e.Property(x => x.IssuedOn).HasColumnName("issued_on");
            e.Property(x => x.DueOn).HasColumnName("due_on");
            e.Property(x => x.SubtotalCents).HasColumnName("subtotal_cents");
            e.Property(x => x.TaxCents).HasColumnName("tax_cents");
            e.Property(x => x.TotalCents).HasColumnName("total_cents");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.SentAt).HasColumnName("sent_at");
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId);
        });

        b.Entity<InvoiceLine>(e =>
        {
            e.ToTable("invoice_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            e.Property(x => x.Position).HasColumnName("position");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
            e.Property(x => x.UnitPriceCents).HasColumnName("unit_price_cents");
            e.Property(x => x.TaxRateBp).HasColumnName("tax_rate_bp");
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            e.Property(x => x.AmountCents).HasColumnName("amount_cents");
            e.Property(x => x.Method).HasColumnName("method");
            e.Property(x => x.Reference).HasColumnName("reference");
            e.Property(x => x.PaidAt).HasColumnName("paid_at");
        });

        b.Entity<EmailLogEntry>(e =>
        {
            e.ToTable("email_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            e.Property(x => x.ToAddress).HasColumnName("to_address");
            e.Property(x => x.Subject).HasColumnName("subject");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Error).HasColumnName("error");
            e.Property(x => x.QueuedAt).HasColumnName("queued_at");
            e.Property(x => x.SentAt).HasColumnName("sent_at");
        });

        // Tenant scoping. Platform-level services run outside a tenant scope.
        b.Entity<Customer>().HasQueryFilter(x => _tenant.TenantId == Guid.Empty || x.TenantId == _tenant.TenantId);
        b.Entity<Invoice>().HasQueryFilter(x => _tenant.TenantId == Guid.Empty || x.TenantId == _tenant.TenantId);
        b.Entity<InvoiceLine>().HasQueryFilter(x => _tenant.TenantId == Guid.Empty || x.TenantId == _tenant.TenantId);
        b.Entity<Payment>().HasQueryFilter(x => _tenant.TenantId == Guid.Empty || x.TenantId == _tenant.TenantId);
        b.Entity<EmailLogEntry>().HasQueryFilter(x => _tenant.TenantId == Guid.Empty || x.TenantId == _tenant.TenantId);
        b.Entity<TenantSettings>().HasQueryFilter(x => _tenant.TenantId == Guid.Empty || x.TenantId == _tenant.TenantId);
    }
}
