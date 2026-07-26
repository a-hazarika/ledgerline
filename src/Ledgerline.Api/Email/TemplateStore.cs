using System.Collections.Concurrent;
using System.Text;

namespace Ledgerline.Api.Email;

/// <summary>
/// Loads email templates from disk and keeps the parsed form around, since the
/// same handful of templates is rendered for every outbound message.
/// </summary>
public sealed class TemplateStore
{
    private readonly ConcurrentDictionary<string, CompiledTemplate> _compiled = new(StringComparer.Ordinal);
    private readonly string _templateRoot;
    private readonly string _assetRoot;

    public TemplateStore(IWebHostEnvironment environment)
    {
        _templateRoot = Path.Combine(environment.ContentRootPath, "Templates");
        _assetRoot = Path.Combine(environment.WebRootPath ?? environment.ContentRootPath, "branding");
    }

    public async ValueTask<CompiledTemplate> GetTemplateAsync(string name, CancellationToken cancellationToken)
    {
        if (_compiled.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(_templateRoot, name);
        var source = await File.ReadAllTextAsync(path, cancellationToken);
        var template = CompiledTemplate.Parse(source);
        return _compiled.GetOrAdd(name, template);
    }

    /// <summary>
    /// Reads a branding asset and returns it as a data URI. Mail clients block remote
    /// images by default, so the logo has to travel with the message.
    /// </summary>
    public async Task<string> ReadInlineAssetAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_assetRoot, fileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(_assetRoot, "default.svg");
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var mediaType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
    }
}

/// <summary>
/// A template split into literal spans and <c>{{placeholder}}</c> slots. Substitution is
/// deliberately dumb: no loops, no conditionals, no expressions.
/// </summary>
public sealed class CompiledTemplate
{
    private readonly IReadOnlyList<Segment> _segments;

    private CompiledTemplate(IReadOnlyList<Segment> segments) => _segments = segments;

    public static CompiledTemplate Parse(string source)
    {
        var segments = new List<Segment>();
        var index = 0;

        while (index < source.Length)
        {
            var open = source.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0)
            {
                segments.Add(new Segment(source[index..], IsPlaceholder: false));
                break;
            }

            var close = source.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                segments.Add(new Segment(source[index..], IsPlaceholder: false));
                break;
            }

            if (open > index)
            {
                segments.Add(new Segment(source[index..open], IsPlaceholder: false));
            }

            segments.Add(new Segment(source[(open + 2)..close].Trim(), IsPlaceholder: true));
            index = close + 2;
        }

        return new CompiledTemplate(segments);
    }

    public string Render(IReadOnlyDictionary<string, string> values)
    {
        var builder = new StringBuilder();

        foreach (var segment in _segments)
        {
            if (!segment.IsPlaceholder)
            {
                builder.Append(segment.Text);
            }
            else if (values.TryGetValue(segment.Text, out var value))
            {
                builder.Append(value);
            }
        }

        return builder.ToString();
    }

    private readonly record struct Segment(string Text, bool IsPlaceholder);
}
