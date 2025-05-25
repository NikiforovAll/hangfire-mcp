using Hangfire;
using HangfireJobs;
using Nall.Hangfire.Mcp;
using Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddHangfireServer();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<ITimeJob, TimeJob>();
builder.Services.AddTransient<ISendMessageJob, SendMessageJob>();
builder.Services.AddHangfireMcp();
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseHttpsRedirection();

app.MapHangfireDashboard(string.Empty);

app.MapPost(
    "/jobs",
    (JobDescriptor jobDescriptor, IHangfireDynamicScheduler scheduler) =>
    {
        var jobId = scheduler.Enqueue(jobDescriptor, typeof(ITimeJob).Assembly);

        return Results.Ok($"Job executed successfully with ID: {jobId}");
    }
);

app.MapGet(
    "/jobs",
    (IHangfireDynamicScheduler scheduler, string? searchTerm) =>
    {
        var jobs = scheduler.DiscoverJobs(
            type =>
                type.IsInterface && type.Name.EndsWith("Job", StringComparison.OrdinalIgnoreCase),
            (type, method) =>
                string.IsNullOrEmpty(searchTerm)
                || $"{type.Name}.{method.Name}".Contains(
                    searchTerm,
                    StringComparison.OrdinalIgnoreCase
                ),
            typeof(ITimeJob).Assembly
        );

        return Results.Ok(jobs);
    }
);

app.Run();
