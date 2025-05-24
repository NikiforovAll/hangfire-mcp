namespace HangfireJobs;

using Microsoft.Extensions.Logging;

public class UserJob(ILogger<UserJob> logger) : IUserJob
{
    public Task ExecuteAsync(string userName)
    {
        logger.LogInformation("User: {UserName}", userName);
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(Payload payload)
    {
        logger.LogInformation("User: {UserName}, Age: {Age}", payload.UserName, payload.Age);
        return Task.CompletedTask;
    }
}
