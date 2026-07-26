using Ledgerline.Api.Email;
using Xunit;

namespace Ledgerline.Api.Tests;

public class CompiledTemplateTests
{
    [Fact]
    public void Substitutes_known_placeholders()
    {
        var template = CompiledTemplate.Parse("Hello {{customer.name}}, invoice {{invoice.number}}.");

        var result = template.Render(new Dictionary<string, string>
        {
            ["customer.name"] = "Halcyon Robotics",
            ["invoice.number"] = "INV-01042"
        });

        Assert.Equal("Hello Halcyon Robotics, invoice INV-01042.", result);
    }

    [Fact]
    public void Drops_placeholders_with_no_value()
    {
        var template = CompiledTemplate.Parse("[{{missing}}]");

        Assert.Equal("[]", template.Render(new Dictionary<string, string>()));
    }

    [Fact]
    public void Tolerates_whitespace_inside_the_braces()
    {
        var template = CompiledTemplate.Parse("{{  brand.name  }}");

        Assert.Equal("Atlas Freight", template.Render(new Dictionary<string, string>
        {
            ["brand.name"] = "Atlas Freight"
        }));
    }

    [Fact]
    public void Leaves_an_unterminated_placeholder_alone()
    {
        var template = CompiledTemplate.Parse("total {{invoice.total");

        Assert.Equal("total {{invoice.total", template.Render(new Dictionary<string, string>()));
    }
}
