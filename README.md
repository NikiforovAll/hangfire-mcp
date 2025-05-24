# Hangfire MCP [![Build](https://github.com/NikiforovAll/hangfire-mcp/actions/workflows/build.yml/badge.svg)](https://github.com/NikiforovAll/hangfire-mcp/actions/workflows/build.yml)

Enqueue background jobs using Hangfire MCP server. In this case, MCP Server uses `IBackgroundJobClient` to enqueue jobs.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.WithMcpServer(args).WithToolsFromAssembly();
builder.Services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(options =>
    options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("hangfire")))
);
builder.Services.AddHangfireMcp();
builder.Services.AddTransient<HangfireTool>();
var app = builder.Build();
app.MapMcpServer(args);
app.Run();
```

Here is implementation of a Hangfire tool:

```csharp
[McpServerToolType]
public class HangfireTool(IHangfireDynamicScheduler scheduler)
{
    [McpServerTool(Name = "RunJob")]
    public string Run(
        [Required] string jobName,
        [Required] string methodName,
        Dictionary<string, object>? parameters = null
    )
    {
        var descriptor = new JobDescriptor(jobName, methodName, parameters);
        return scheduler.Enqueue(descriptor, typeof(ITimeJob).Assembly);
    }
}
```
