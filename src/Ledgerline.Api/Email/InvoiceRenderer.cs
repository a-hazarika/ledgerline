using System.Net;
using System.Text;
using Ledgerline.Api.Domain;
using Microsoft.Extensions.Options;

namespace Ledgerline.Api.Email;

public interface IInvoiceRenderer
{
    /// <summary>Sets the brand applied to documents rendered from here on.</summary>
    void ApplyBranding(BrandProfile brand);

    Task<RenderedEmail> RenderInvoiceAsync(
        InvoiceView invoice,
        AccountSummary summary,
        CancellationToken cancellationToken);
}

public sealed class InvoiceRenderer : IInvoiceRenderer
{
    private const string TemplateName = "invoice.html";

    private readonly TemplateStore _templates;
    private readonly EmailOptions _options;

    private BrandProfile _brand = BrandProfile.Fallback;

    public InvoiceRenderer(TemplateStore templates, IOptions<EmailOptions> options)
    {
        _templates = templates;
        _options = options.Value;
    }

    public void ApplyBranding(BrandProfile brand) => _brand = brand;

    public async Task<RenderedEmail> RenderInvoiceAsync(
        InvoiceView invoice,
        AccountSummary summary,
        CancellationToken cancellationToken)
    {
        var template = await _templates.GetTemplateAsync(TemplateName, cancellationToken);
        var logo = await _templates.ReadInlineAssetAsync(_brand.LogoFile, cancellationToken);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brand.name"] = Encode(_brand.LegalName),
            ["brand.color"] = _brand.AccentColor,
            ["brand.logo"] = logo,
            ["brand.remit"] = Encode(_brand.RemitTo),
            ["brand.footer"] = Encode(_brand.Footer),

            ["invoice.number"] = Encode(invoice.Number),
            ["invoice.issued"] = invoice.IssuedOn.ToString("d MMM yyyy"),
            ["invoice.due"] = invoice.DueOn.ToString("d MMM yyyy"),
            ["invoice.subtotal"] = Money.Format(invoice.SubtotalCents, invoice.Currency),
            ["invoice.tax"] = Money.Format(invoice.TaxCents, invoice.Currency),
            ["invoice.total"] = Money.Format(invoice.TotalCents, invoice.Currency),
            ["invoice.lines"] = BuildLineRows(invoice),

            ["customer.name"] = Encode(invoice.CustomerName),

            ["summary.count"] = summary.OpenInvoiceCount.ToString(),
            ["summary.balance"] = Money.Format(summary.OpenBalanceCents, invoice.Currency)
        };

        return new RenderedEmail(
            FromName: _brand.LegalName,
            FromAddress: _options.FromAddress,
            ReplyTo: _brand.ReplyTo,
            Subject: $"Invoice {invoice.Number} from {_brand.LegalName}",
            HtmlBody: template.Render(values));
    }

    private static string BuildLineRows(InvoiceView invoice)
    {
        var rows = new StringBuilder();

        foreach (var line in invoice.Lines.OrderBy(l => l.Position))
        {
            rows.Append("<tr>")
                .Append("<td class=\"desc\">").Append(Encode(line.Description)).Append("</td>")
                .Append("<td class=\"num\">").Append(line.Quantity.ToString("0.###")).Append("</td>")
                .Append("<td class=\"num\">").Append(Money.Format(line.UnitPriceCents, invoice.Currency)).Append("</td>")
                .Append("<td class=\"num\">").Append(Money.Format(line.AmountCents, invoice.Currency)).Append("</td>")
                .Append("</tr>");
        }

        return rows.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
