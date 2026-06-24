using System.Collections.Concurrent;
using System.Threading.Channels;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Worker.Pipeline;

/// <summary>
/// In-process producer/consumer queue for scan ids, built on
/// <see cref="System.Threading.Channels"/>. A bounded channel gives natural
/// backpressure, and an in-flight set keeps the DB poller from enqueuing the same
/// scan twice while it is still being processed.
/// </summary>
internal sealed class ScanDispatchQueue
{
    private readonly Channel<ScanId> _channel =
        Channel.CreateBounded<ScanId>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true,
        });

    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

    /// <summary>The reader consumers drain.</summary>
    public ChannelReader<ScanId> Reader => _channel.Reader;

    /// <summary>
    /// Enqueues a scan unless it is already in flight. Returns <see langword="false"/>
    /// if it was a duplicate.
    /// </summary>
    public async ValueTask<bool> TryEnqueueAsync(ScanId id, CancellationToken cancellationToken)
    {
        if (!_inFlight.TryAdd(id.Value, 0))
        {
            return false;
        }

        await _channel.Writer.WriteAsync(id, cancellationToken);
        return true;
    }

    /// <summary>Marks a scan as no longer in flight once processing has finished.</summary>
    public void Release(ScanId id) => _inFlight.TryRemove(id.Value, out _);
}
