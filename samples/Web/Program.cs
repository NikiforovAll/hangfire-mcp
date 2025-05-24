using System.Globalization;
using Hangfire;
using Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddHangfireServer();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<TimeJob>();
builder.Services.AddTransient<ITimeJob, TimeJob>();
builder.Services.AddTransient<ITimeJobWithParameters, TimeJob>();
builder.Services.AddTransient<HangfireDynamicScheduler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
var app = builder.Build();
app.UseHttpsRedirection();

app.MapPost(
    "/jobs",
    (JobDescriptor jobDescriptor, HangfireDynamicScheduler scheduler) =>
    {
        var jobId = scheduler.Enqueue(jobDescriptor);

        return Results.Ok($"Job executed successfully with ID: {jobId}");
    }
);

app.MapGet(
    "/timeJob",
    (HangfireDynamicScheduler scheduler) =>
    {
        var jobId = scheduler.Enqueue(
            new(
                typeof(ITimeJob).FullName!,
                nameof(ITimeJob.ExecuteAsync),
                new Dictionary<string, object> { ["userName"] = "John Doe" }
            )
        );
        return Results.Ok($"Job executed successfully with ID: {jobId}");
    }
);

app.MapGet(
    "/timeJobClient",
    (IBackgroundJobClient client) =>
    {
        client.Enqueue<TimeJob>(x => x.ExecuteAsync("John Doe"));
        return Results.Ok("Time job executed successfully.");
    }
);

app.MapHangfireDashboard(string.Empty);

app.Run();

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

public interface ITimeJob
{
    public Task ExecuteAsync();
}

public interface ITimeJobWithParameters
{
    public Task ExecuteAsync(Payload payload);
    public Task ExecuteAsync(string userName);
}

public class Payload
{
    public string UserName { get; set; } = string.Empty;
    public int Age { get; set; }
}
