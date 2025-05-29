namespace HangfireMCP;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Hangfire;
using Nall.Hangfire.Mcp;

[McpServerToolType]
public class HangfireTool(
    IHangfireDynamicScheduler scheduler,
    IBackgroundJobClient backgroundJobClient,
    DynamicJobLoader dynamicJobLoader,
    ILogger<HangfireTool> logger
)
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

        var assembly =
            dynamicJobLoader.GetAssembly()
            ?? throw new InvalidOperationException(
                "Dynamic job loader is not initialized or assembly is not loaded."
            );
        return scheduler.Enqueue(descriptor, assembly);
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
        var jobsList = new List<JobDescriptor>();
        try
        {
            var dynamicJobs = dynamicJobLoader.DiscoverJobs();
            if (dynamicJobs.Any())
            {
                // Filter dynamic jobs by search term if provided
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    dynamicJobs = dynamicJobs.Where(job =>
                        $"{job.JobName}.{job.MethodName}".Contains(
                            searchTerm,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                }

                jobsList.AddRange(dynamicJobs);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error discovering dynamic jobs");
        }

        return JsonSerializer.Serialize(
            jobsList.Select(job => new
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

    [McpServerTool(Name = "RequeueJob"), Description("Requeues an existing job by ID")]
    [return: Description("The new job ID if successful, null if the original job was not found")]
    public string? RequeueJob([Required, Description("The job ID to requeue")] string jobId)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        backgroundJobClient.Requeue(jobId);

        return this.GetJobById(jobId);
    }
}
