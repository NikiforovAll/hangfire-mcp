using HangfireMCP;
using Nall.Hangfire.Mcp;

var builder = WebApplication.CreateBuilder(args);
builder.WithMcpServer(args).WithToolsFromAssembly();
builder.AddHangfire();
builder.Services.AddHangfireMcp();
builder.Services.AddTransient<HangfireTool>();
builder.Services.AddHostedService<DynamicJobLoaderHostedService>();

var app = builder.Build();
app.MapMcpServer(args);
app.Run();
