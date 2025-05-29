# Hangfire MCP [![Build](https://github.com/NikiforovAll/hangfire-mcp/actions/workflows/build.yml/badge.svg)](https://github.com/NikiforovAll/hangfire-mcp/actions/workflows/build.yml)

Enqueue background jobs using Hangfire MCP server.

## Motivation

Interaction with *Hangfire* using *Hangfire MCP Server* allows you to enqueue jobs from any client that supports MCP protocol.
For example, you can use Hangfire MCP directly from *VS Code* in *Agent Mode* and enqueue jobs. It makes possible to execute any kind of code without writing additional code.

<video src="https://github.com/user-attachments/assets/e6abc036-b1f9-4691-a829-65292db5b5e6" controls="controls"></video>

Here is MCP Server configuration for VS Code:

```json
{
    "servers": {
        "hangfire-mcp": {
            "url": "http://localhost:3001"
        }
    }
}
```

## Code Example

Here is how it works:

```mermaid
sequenceDiagram
    participant User as User
    participant MCPHangfire as MCP Hangfire
    participant IBackgroundJobClient as IBackgroundJobClient
    participant Database as Database
    participant HangfireServer as Hangfire Server

    User->>MCPHangfire: Enqueue Job
    MCPHangfire->>IBackgroundJobClient: Send Job Message
    IBackgroundJobClient->>Database: Store Job Message
    HangfireServer->>Database: Fetch Job Message
    HangfireServer->>HangfireServer: Process Job
```

## Standalone Mode

It is a regular MCP packaged as .NET global tool. Here is how to setup it as an MCP server in VSCode.

```bash
 dotnet tool install --global --add-source Nall.HangfireMCP
```

Configuration:

```json
{
  "servers": {
    "hangfire-mcp-standalone": {
      "type": "stdio",
      "command": "HangfireMCP",
      "args": [
        "--stdio"
      ],
      "env": {
        "HANGFIRE_JOBS_ASSEMBLY": "path/to/Jobs.dll",
        "HANGFIRE_JOBS_MATCH_EXPRESSION": "[?IsInterface && contains(Name, 'Job')]",
        "HANGFIRE_CONNECTION_STRING": "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=hangfire"
      }
    }
  }
}
```

### Aspire

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder
    .AddPostgres("postgres-server")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var postgresDatabase = postgresServer.AddDatabase("hangfire");

builder.AddProject<Projects.Web>("server")
    .WithReference(postgresDatabase)
    .WaitFor(postgresDatabase);

var mcp = builder
    .AddProject<Projects.HangfireMCP_Standalone>("hangfire-mcp")
    .WithEnvironment("HANGFIRE_JOBS_ASSEMBLY", "path/to/Jobs.dll")
    .WithEnvironment("HANGFIRE_JOBS_MATCH_EXPRESSION", "[?IsInterface && contains(Name, 'Job')]")
    .WithReference(postgresDatabase)
    .WaitFor(postgresDatabase);

builder
    .AddMCPInspector()
    .WithSSE(mcp)
    .WaitFor(mcp);

builder.Build().Run();
```

As result, the jobs are dynamically loaded from the specified assembly and can be enqueued using MCP protocol. The rules for matching job names can be specified using `HANGFIRE_JOBS_MATCH_EXPRESSION` environment variable. For example, the expression `[?IsInterface && contains(Name, 'Job')]` will match all interfaces that contain "Job" in their name. It is a [JMESPath](https://jmespath.org/tutorial.html) expression, so you can define how to match job names according to your needs.

## Custom Setup (as Code) Mode

You can create your own MCP server and use this project as starting point. You can extend it with your own tools and features. Here is an example of how to set up Hangfire MCP server in a custom project.

### Aspire

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder
    .AddPostgres("postgres-server")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var postgresDatabase = postgresServer.AddDatabase("hangfire");

builder.AddProject<Projects.Web>("server")
    .WithReference(postgresDatabase)
    .WaitFor(postgresDatabase);

var mcp = builder
    .AddProject<Projects.HangfireMCP>("hangfire-mcp")
    .WithReference(postgresDatabase)
    .WaitFor(postgresDatabase);

builder
    .AddMCPInspector()
    .WithSSE(mcp)
    .WaitFor(mcp);

builder.Build().Run();
```

![Aspire Dashboard](assets/aspire-dashboard.png)

### MCP Server

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

Here is an example of the Hangfire tool:

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

## Tools

![Inspector](assets/inspector.png)
