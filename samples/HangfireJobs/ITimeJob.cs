namespace HangfireJobs;

using System.Globalization;
using Microsoft.Extensions.Logging;

public interface ITimeJob
{
    public Task ExecuteAsync();
}

public class TimeJob(TimeProvider timeProvider, ILogger<TimeJob> logger) : ITimeJob
{
    public Task ExecuteAsync()
    {
        logger.LogInformation(
            "Current time: {CurrentTime}",
            timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        );
        return Task.CompletedTask;
    }
}
