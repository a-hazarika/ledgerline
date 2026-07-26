using System.Threading.Channels;

namespace Ledgerline.Api.Email;

public interface IEmailQueue
{
    void Enqueue(InvoiceEmailJob job);
    IAsyncEnumerable<InvoiceEmailJob> ReadAllAsync(CancellationToken cancellationToken);
    int Depth { get; }
}

/// <summary>
/// In-process hand-off between the API and the sender worker. Bounded so a runaway
/// caller applies backpressure instead of eating the heap.
/// </summary>
public sealed class ChannelEmailQueue : IEmailQueue
{
    private readonly Channel<InvoiceEmailJob> _channel = Channel.CreateBounded<InvoiceEmailJob>(
        new BoundedChannelOptions(2048)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public int Depth => _channel.Reader.Count;

    public void Enqueue(InvoiceEmailJob job)
    {
        if (!_channel.Writer.TryWrite(job))
        {
            throw new InvalidOperationException("Outbound email queue is full.");
        }
    }

    public IAsyncEnumerable<InvoiceEmailJob> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
