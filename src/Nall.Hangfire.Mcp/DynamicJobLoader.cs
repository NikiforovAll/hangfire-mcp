namespace Nall.Hangfire.Mcp;

using System.Reflection;
using DevLab.JmesPath;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides functionality for dynamically loading and filtering job types from external assemblies.
/// </summary>
public class DynamicJobLoader(IHangfireDynamicScheduler scheduler, ILogger<DynamicJobLoader> logger)
{
    private string? assemblyPath;

    public Assembly? LoadedAssembly { get; set; }

    /// <summary>
    /// Initializes the dynamic job loader with assembly path and JMESPath expression from environment variables.
    /// </summary>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    public bool Initialize()
    {
        try
        {
            // Get assembly path from environment variable
            this.assemblyPath = Environment.GetEnvironmentVariable("HANGFIRE_JOBS_ASSEMBLY");
            if (string.IsNullOrWhiteSpace(this.assemblyPath))
            {
                logger.LogWarning("HANGFIRE_JOBS_ASSEMBLY environment variable not set or empty");
                return false;
            }

            // Attempt to load the assembly
            if (!this.LoadAssembly())
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize dynamic job loader");
            return false;
        }
    }

    /// <summary>
    /// Loads the assembly from the configured path.
    /// </summary>
    /// <returns>True if assembly loaded successfully, false otherwise.</returns>
    private bool LoadAssembly()
    {
        if (string.IsNullOrEmpty(this.assemblyPath))
        {
            return false;
        }

        try
        {
            if (!File.Exists(this.assemblyPath))
            {
                logger.LogError("Assembly file not found: {Path}", this.assemblyPath);
                return false;
            }

            // Load the assembly
            this.LoadedAssembly = Assembly.LoadFrom(this.assemblyPath);
            logger.LogInformation(
                "Successfully loaded assembly: {AssemblyName} from {Path}",
                this.LoadedAssembly.FullName,
                this.assemblyPath
            );

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load assembly from {Path}", this.assemblyPath);
            this.LoadedAssembly = null;
            return false;
        }
    }

    /// <summary>
    /// Discovers job types in the loaded assembly that match the JMESPath expression.
    /// </summary>
    /// <returns>A collection of job descriptors for the matched types.</returns>
    public IEnumerable<JobDescriptor> DiscoverJobs()
    {
        if (this.LoadedAssembly == null)
        {
            logger.LogWarning("No assembly loaded. Attempting to initialize...");
            if (!this.Initialize() || this.LoadedAssembly == null)
            {
                logger.LogError("Failed to initialize or load assembly. Aborting job discovery.");
                return [];
            }
        }

        try
        {
            // Get JMESPath expression from environment variable
            var expressionString = Environment.GetEnvironmentVariable(
                "HANGFIRE_JOBS_MATCH_EXPRESSION"
            );
            if (string.IsNullOrWhiteSpace(expressionString))
            {
                logger.LogWarning(
                    "HANGFIRE_JOBS_MATCH_EXPRESSION environment variable not set or empty"
                );
                return [];
            }

            // Create JmesPath expression
            var jmesPath = new JmesPath();

            // Build a collection of type information as JObject to use with JMESPath
            var types = this
                .LoadedAssembly.GetTypes()
                .Select(type => new
                {
                    type.Name,
                    type.FullName,
                    type.Namespace,
                    type.IsPublic,
                    type.IsClass,
                    type.IsInterface,
                })
                .ToList();

            // Serialize to JSON for JmesPath to process
            var typesJson = System.Text.Json.JsonSerializer.Serialize(types);

            // Apply JmesPath expression to filter the types
            var result = jmesPath.Transform(typesJson, expressionString);

            if (string.IsNullOrEmpty(result) || result == "null" || result == "[]")
            {
                logger.LogWarning(
                    "JMESPath expression '{Expression}' did not match any types",
                    expressionString
                );
                return [];
            }

            // Deserialize the filtered results
            var filteredTypeInfo = System.Text.Json.JsonSerializer.Deserialize<
                List<System.Text.Json.JsonElement>
            >(result);

            if (filteredTypeInfo == null || filteredTypeInfo.Count == 0)
            {
                logger.LogWarning("No types matched the JMESPath expression");
                return [];
            }

            // Get the full names of the matched types
            var matchedFullNames = filteredTypeInfo
                .Select(element => element.GetProperty("FullName").GetString())
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            // Discover jobs from these types
            var jobs = scheduler.DiscoverJobs(
                type => matchedFullNames.Contains(type.FullName),
                assembly: this.LoadedAssembly
            );

            logger.LogInformation(
                "Discovered {Count} jobs from {AssemblyName}",
                jobs.Count(),
                this.LoadedAssembly.GetName().Name
            );

            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error discovering jobs with JMESPath expression");
            return [];
        }
    }

    /// <summary>
    /// Gets the loaded assembly.
    /// </summary>
    /// <returns>The loaded assembly or null if no assembly is loaded.</returns>
    public Assembly? GetAssembly() => this.LoadedAssembly;
}
