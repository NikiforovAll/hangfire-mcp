namespace HangfireMCP;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Hangfire;
using HangfireJobs;
using Nall.Hangfire.Mcp;

[McpServerToolType]
public class HangfireTool(IHangfireDynamicScheduler scheduler)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [
        McpServerTool(Name = "RunJob"),
        Description("Invokes a job with the given jobName, methodName, and parameters")
    ]
    [return: Description("The job ID of the enqueued job")]
    public string Run(
        [Required] string jobName,
        [Required] string methodName,
        Dictionary<string, object>? parameters = null
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
        [Description(
            "The search term to filter job names, optional parameter, use it only when users want to search for jobs"
        )]
            string? searchTerm = null
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

        return JsonSerializer.Serialize(
            jobs.Select(job => new
            {
                job.JobName,
                job.MethodName,
                job.Parameters,
            }),
            JsonOptions
        );
    }

    [McpServerTool(Name = "GetJobById"), Description("Gets job details by job ID")]
    [return: Description("The job details as JSON, or null if not found")]
    public string? GetJobById([Required, Description("The job ID")] string jobId)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        using var connection = JobStorage.Current.GetConnection();
        var jobData = connection.GetJobData(jobId);
        if (jobData == null)
        {
            return null;
        }

        var result = new
        {
            JobId = jobId,
            jobData.State,
            jobData.CreatedAt,
            Arguments = jobData.Job?.Args,
            Method = jobData.Job?.Method?.Name,
            Type = jobData.Job?.Type?.FullName,
        };
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
