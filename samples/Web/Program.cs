using Hangfire;
using HangfireJobs;
using Nall.Hangfire.Mcp;
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
        var jobId = scheduler.Enqueue(jobDescriptor, typeof(ITimeJob).Assembly);

        return Results.Ok($"Job executed successfully with ID: {jobId}");
    }
);

app.MapHangfireDashboard(string.Empty);

app.Run();
