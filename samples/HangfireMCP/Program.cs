using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HangfireJobs;
using HangfireMCP;
using Nall.Hangfire.Mcp;

var builder = WebApplication.CreateBuilder(args);
builder.WithMcpServer(args).WithToolsFromAssembly();
builder.AddHangfire();
builder.Services.AddHangfireMcp();
builder.Services.AddTransient<HangfireTool>();
var app = builder.Build();
app.MapMcpServer(args);
app.Run();

[McpServerToolType]
public class HangfireTool(IHangfireDynamicScheduler scheduler)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [McpServerTool(Name = "RunJob"), Description("Invokes a job with the given jobName, methodName, and parameters")]
    [return: Description("The job ID of the enqueued job")]
    public string Run(
        [Required, Description("The name of the job to run")] string jobName,
        [Required, Description("The name of the method to invoke")] string methodName,
        [Description("The parameters for the job")] Dictionary<string, object>? parameters = null
    )
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(methodName);

        var descriptor = new JobDescriptor(jobName, methodName, parameters);
        return scheduler.Enqueue(descriptor, typeof(ITimeJob).Assembly);
    }

    [McpServerTool(Name = "ListJobs"), Description("Lists all jobs")]
    [return: Description("An array of job descriptors in JSON format")]
    public string ListJobs(
        [Description("The search term to filter job names")] string? searchTerm = null
    )
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

        JobDescriptorResponse[] descriptors =
        [
            .. jobs.Select(job => new JobDescriptorResponse
            {
                JobName = job.JobName,
                MethodName = job.MethodName,
                Parameters = job.Parameters,
            }),
        ];
        return JsonSerializer.Serialize(descriptors, JsonOptions);
    }
}

public class JobDescriptorResponse
{
    [Description("The job name (jobName)")]
    public string JobName { get; set; } = string.Empty;

    [Description("The method name (methodName)")]
    public string MethodName { get; set; } = string.Empty;

    [Description("The parameters for the job (parameters)")]
    public IDictionary<string, object>? Parameters { get; set; }
}
