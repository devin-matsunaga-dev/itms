using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Itms.Messaging.Outbox;

/// <summary>
/// The background loop that drives <see cref="IOutboxProcessor"/>.
/// </summary>
/// <remarks>
/// It holds no logic of its own beyond pacing: a pass that found a full batch loops
/// immediately, because there is probably more waiting, and a pass that found nothing
/// waits <see cref="MessagingOptions.PollInterval"/>. A pass that throws — the database
/// is down, say — is logged and retried on the next interval rather than being allowed
/// to take the host down with it.
/// </remarks>
/// <param name="scopeFactory">Creates the per-pass scope, which owns its own connection.</param>
/// <param name="options">Pacing settings.</param>
/// <param name="logger">Structured log sink.</param>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly MessagingOptions _options = options.Value;
    private readonly ILogger<OutboxDispatcher> _logger = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.DispatcherStarted(_options.BatchSize, _options.PollInterval);

        using var timer = new PeriodicTimer(_options.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            int dispatched;
            try
            {
                dispatched = await RunPassAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.DispatcherPassFailed(exception, _options.PollInterval);
                dispatched = 0;
            }

            // A full batch means there is very likely more behind it. Waiting out the poll
            // interval in that case would cap throughput at BatchSize per interval.
            if (dispatched >= _options.BatchSize)
            {
                continue;
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }

        _logger.DispatcherStopped();
    }

    private async Task<int> RunPassAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
        return await processor.ProcessOnceAsync(cancellationToken).ConfigureAwait(false);
    }
}
