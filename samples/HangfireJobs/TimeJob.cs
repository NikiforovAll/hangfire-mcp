namespace HangfireJobs;

using System.Globalization;
using Microsoft.Extensions.Logging;

public class TimeJob(TimeProvider timeProvider, ILogger<TimeJob> logger)
    : ITimeJob,
        ITimeJobWithParameters
{
    public Task ExecuteAsync()
    {
        logger.LogInformation(
            "Current time: {CurrentTime}",
            timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        );
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(string userName)
    {
        logger.LogInformation(
            "Current time: {CurrentTime}, User: {UserName}",
            timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            userName
        );
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(Payload payload)
    {
        logger.LogInformation(
            "Current time: {CurrentTime}, User: {UserName}, Age: {Age}",
            timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            payload.UserName,
            payload.Age
        );
        return Task.CompletedTask;
    }
}
