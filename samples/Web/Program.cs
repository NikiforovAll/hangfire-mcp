using Hangfire;
using Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddHangfireServer();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<TimeJob>();
builder.Services.AddTransient<HangfireDynamicScheduler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
var app = builder.Build();
app.UseHttpsRedirection();

app.MapPost(
    "/jobs",
    (JobDescriptor jobDescriptor, HangfireDynamicScheduler scheduler) =>
    {
        var jobId = scheduler.EnqueueByTypeName(jobDescriptor.TypeName, jobDescriptor.MethodName);

        return Results.Ok($"Job executed successfully with ID: {jobId}");
    }
);

app.MapGet(
    "/timeJob",
    (HangfireDynamicScheduler scheduler) =>
    {
        var jobId = scheduler.EnqueueByTypeName(
            typeof(TimeJob).FullName!,
            nameof(TimeJob.ExecuteAsync)
        );
        return Results.Ok($"Job executed successfully with ID: {jobId}");
    }
);

app.MapGet(
    "/timeJobClient",
    (IBackgroundJobClient client) =>
    {
        client.Enqueue<TimeJob>(x => x.ExecuteAsync());
        return Results.Ok("Time job executed successfully.");
    }
);

app.MapHangfireDashboard(string.Empty);

app.Run();

public class JobDescriptor
{
    public string TypeName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
}

public class TimeJob(TimeProvider timeProvider)
{
    public Task ExecuteAsync()
    {
        Console.WriteLine($"Current time: {timeProvider.GetUtcNow():yyyy-MM-dd HH:mm:ss}");
        return Task.CompletedTask;
    }
}
