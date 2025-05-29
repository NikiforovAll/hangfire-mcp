namespace Nall.Hangfire.Mcp;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that initializes and manages the dynamic job loader.
/// </summary>
public class DynamicJobLoaderHostedService(
    DynamicJobLoader dynamicJobLoader,
    ILogger<DynamicJobLoaderHostedService> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting dynamic job loader service");

        try
        {
            var initialized = dynamicJobLoader.Initialize();
            if (initialized)
            {
                logger.LogInformation("Dynamic job loader initialized successfully");

                // Discover jobs on startup
                var jobs = dynamicJobLoader.DiscoverJobs();
                logger.LogInformation("Discovered {Count} jobs on startup", jobs.Count());
            }
            else
            {
                logger.LogWarning("Dynamic job loader initialization failed or was not configured");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during dynamic job loader startup");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping dynamic job loader service");
        return Task.CompletedTask;
    }
}
