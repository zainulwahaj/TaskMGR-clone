using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TaskMGR.Core.Interfaces;

namespace TaskMGR.UI.Services;

/// <summary>
/// Periodically polls the active platform service and publishes the latest snapshot to a bounded channel.
/// </summary>
public sealed class ProcessRefreshService : IAsyncDisposable
{
    private readonly IPlatformService _platform;
    private readonly Channel<RefreshResult> _channel;
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _disposeCts = new();
    private Task? _producerTask;

    /// <summary>
    /// Gets a reader that exposes the most recent refresh results.
    /// </summary>
    public ChannelReader<RefreshResult> Updates => _channel.Reader;

    /// <summary>
    /// Initializes a new refresh pipeline.
    /// </summary>
    /// <param name="platform">The platform service to poll.</param>
    /// <param name="interval">The polling interval.</param>
    public ProcessRefreshService(IPlatformService platform, TimeSpan interval)
    {
        _platform = platform;
        _timer = new PeriodicTimer(interval);
        _channel = Channel.CreateBounded<RefreshResult>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });
    }

    /// <summary>
    /// Starts producing refresh snapshots.
    /// </summary>
    public void Start()
    {
        if (_producerTask is not null)
        {
            return;
        }

        _producerTask = Task.Run(ProduceAsync);
    }

    private async Task ProduceAsync()
    {
        try
        {
            await PublishSnapshotAsync(_disposeCts.Token).ConfigureAwait(false);

            while (await _timer.WaitForNextTickAsync(_disposeCts.Token).ConfigureAwait(false))
            {
                await PublishSnapshotAsync(_disposeCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    private async Task PublishSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var processResult = await _platform.GetProcessesAsync(cancellationToken).ConfigureAwait(false);
            if (!processResult.IsSuccess)
            {
                _channel.Writer.TryWrite(RefreshResult.FromError(processResult.Error));
                return;
            }

            var metrics = await _platform.GetSystemMetricsAsync(cancellationToken).ConfigureAwait(false);

            await _channel.Writer
                .WriteAsync(new RefreshResult(processResult.Value, metrics), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _channel.Writer.TryWrite(RefreshResult.FromError(ex.Message));
        }
    }

    /// <summary>
    /// Stops the producer loop and releases timer resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return;
        }

        _disposeCts.Cancel();
        _timer.Dispose();

        if (_producerTask is not null)
        {
            try
            {
                await _producerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
            {
            }
        }

        _disposeCts.Dispose();
    }
}
