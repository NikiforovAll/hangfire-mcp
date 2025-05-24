namespace Nall.Hangfire.Mcp;

public class JobDescriptor(
    string typeName,
    string methodName,
    IDictionary<string, object>? parameters = null
)
{
    public string TypeName { get; } = typeName;
    public string MethodName { get; } = methodName;
    public IDictionary<string, object>? Parameters { get; } = parameters;
}
