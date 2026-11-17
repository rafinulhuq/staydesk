using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StayDesk.Application.Bookings;

namespace StayDesk.Infrastructure.Jobs;

// BOOK-3: marks expired holds Cancelled once a minute. It only tidies up: an expired hold
// already stops blocking its room (SRCH-3), and booking creation expires any hold it meets.
public class BookingExpiryJob(IServiceScopeFactory scopes, TimeProvider time, ILogger<BookingExpiryJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<BookingService>().ExpireHoldsAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Expired {Count} unpaid bookings", expired);
                }
            }
            // An unhandled exception would stop the job (and the host) for good; one failed run,
            // e.g. while the database restarts, should only skip that minute.
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Expiring unpaid bookings failed");
            }
        }
    }
}
