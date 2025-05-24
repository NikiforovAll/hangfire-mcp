namespace Nall.Hangfire.Mcp;

public class JobDescriptor(
    string jobName,
    string methodName,
    IDictionary<string, object>? parameters = null
)
{
    public string JobName { get; } = jobName;
    public string MethodName { get; } = methodName;
    public IDictionary<string, object>? Parameters { get; } = parameters;
}
