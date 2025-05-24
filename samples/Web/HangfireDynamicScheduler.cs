namespace Web;

using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Hangfire;

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

public class HangfireDynamicScheduler(IBackgroundJobClient client)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Enqueues a job based on the provided <see cref="JobDescriptor"/>.
    /// </summary>
    public string Enqueue(JobDescriptor descriptor, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        assembly ??= Assembly.GetExecutingAssembly();
        var type =
            assembly.GetType(descriptor.TypeName)
            ?? throw new InvalidOperationException($"Type '{descriptor.TypeName}' not found.");

        return type.IsInterface
            ? this.EnqueueByInterfaceName(type, descriptor.MethodName, descriptor.Parameters)
            : this.EnqueueByTypeName(type, descriptor.MethodName, descriptor.Parameters);
    }

    private string EnqueueByInterfaceName(
        Type iface,
        string methodName,
        IDictionary<string, object>? parameters
    )
    {
        // Find the correct method overload based on parameter types
        var method =
            FindMethodByNameAndParameters(iface, methodName, parameters)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' not found on '{iface.FullName}' with matching parameters."
            );

        var param = Expression.Parameter(iface, "x");
        var methodParams = method.GetParameters();
        var arguments = BuildMethodArguments(methodParams, parameters);
        var call = Expression.Call(param, method, arguments);

        var lambdaType = typeof(Func<,>).MakeGenericType(iface, typeof(Task));
        var lambda = Expression.Lambda(lambdaType, call, param);

        var enqueueMethod = GetEnqueueMethod();
        var genericEnqueue = enqueueMethod.MakeGenericMethod(iface);
        return (string)(
            genericEnqueue.Invoke(null, [client, lambda])
            ?? throw new InvalidOperationException("Failed to enqueue job")
        );
    }

    private static MethodInfo? FindMethodByNameAndParameters(
        Type type,
        string methodName,
        IDictionary<string, object>? parameters
    )
    {
        // Get all methods with the given name
        var candidateMethods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
            .Where(m => m.Name == methodName)
            .ToList();

        if (candidateMethods.Count == 0)
        {
            return null;
        }

        // If only one method with this name exists, return it
        if (candidateMethods.Count == 1)
        {
            return candidateMethods[0];
        }

        // If we have parameters, try to find the best match by parameter names
        if (parameters != null && parameters.Count > 0)
        {
            var paramNames = parameters.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find methods where all parameters have matching names
            var matchingMethods = candidateMethods
                .Where(m =>
                {
                    var methodParamNames = m.GetParameters()
                        .Select(p => p.Name)
                        .Where(n => n != null)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // Check if all required parameters (non-optional) have values
                    var requiredParams = m.GetParameters()
                        .Where(p => !p.HasDefaultValue)
                        .Select(p => p.Name)
                        .Where(n => n != null);

                    return requiredParams.All(rp => rp != null && paramNames.Contains(rp));
                })
                .ToList();

            if (matchingMethods.Count == 1)
            {
                return matchingMethods[0];
            }

            // If multiple methods match, select the one with the most matching parameters
            if (matchingMethods.Count > 1)
            {
                return matchingMethods
                    .OrderByDescending(m =>
                    {
                        var methodParamNames = m.GetParameters()
                            .Select(p => p.Name)
                            .Where(n => n != null);

                        return methodParamNames.Count(mpn =>
                            mpn != null && paramNames.Contains(mpn)
                        );
                    })
                    .FirstOrDefault();
            }
        }

        // If we can't find by parameter names, just return the first method
        return candidateMethods.FirstOrDefault();
    }

    private static Expression[] BuildMethodArguments(
        ParameterInfo[] methodParams,
        IDictionary<string, object>? parameters
    )
    {
        var arguments = new Expression[methodParams.Length];
        for (var i = 0; i < methodParams.Length; i++)
        {
            var methodParam = methodParams[i];
            if (parameters != null && parameters.TryGetValue(methodParam.Name!, out var value))
            {
                var convertedValue = value;
                if (value is JsonElement jsonElement)
                {
                    convertedValue = GetValue(methodParam, jsonElement);
                }
                arguments[i] = Expression.Constant(convertedValue, methodParam.ParameterType);
            }
            else if (methodParam.HasDefaultValue)
            {
                arguments[i] = Expression.Constant(
                    methodParam.DefaultValue,
                    methodParam.ParameterType
                );
            }
            else
            {
                throw new ArgumentException(
                    $"Required parameter '{methodParam.Name}' was not provided"
                );
            }
        }
        return arguments;
    }

    private static object? GetValue(ParameterInfo methodParam, JsonElement jsonElement) =>
        jsonElement.ValueKind switch
        {
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number when methodParam.ParameterType == typeof(int) =>
                jsonElement.GetInt32(),
            JsonValueKind.Number when methodParam.ParameterType == typeof(long) =>
                jsonElement.GetInt64(),
            JsonValueKind.Number when methodParam.ParameterType == typeof(double) =>
                jsonElement.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => jsonElement.GetBoolean(),
            JsonValueKind.Object => JsonSerializer.Deserialize(
                jsonElement.GetRawText(),
                methodParam.ParameterType,
                JsonSerializerOptions
            ),
            JsonValueKind.Array => JsonSerializer.Deserialize(
                jsonElement.GetRawText(),
                methodParam.ParameterType,
                JsonSerializerOptions
            ),
            _ => throw new ArgumentException(
                $"Unsupported parameter type '{methodParam.ParameterType}' for '{methodParam.Name}'"
            ),
        };

    private static MethodInfo GetEnqueueMethod()
    {
        return typeof(BackgroundJobClientExtensions)
            .GetMethods()
            .First(m =>
            {
                var p = m.GetParameters()[1].ParameterType;
                if (!p.IsGenericType || p.GetGenericTypeDefinition() != typeof(Expression<>))
                {
                    return false;
                }

                var inner = p.GetGenericArguments()[0];
                return inner.IsGenericType
                    && inner.GetGenericTypeDefinition() == typeof(Func<,>)
                    && inner.GetGenericArguments()[1] == typeof(Task);
            });
    }

    private string EnqueueByTypeName(
        Type type,
        string methodName,
        IDictionary<string, object>? parameters
    )
    {
        // Find the correct method overload based on parameter types
        var method =
            FindMethodByNameAndParameters(type, methodName, parameters)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' not found on type '{type.FullName}' with matching parameters."
            );

        var param = Expression.Parameter(type, "x");
        var methodParams = method.GetParameters();
        var arguments = BuildMethodArguments(methodParams, parameters);
        var call = Expression.Call(param, method, arguments);

        var lambdaType = typeof(Func<,>).MakeGenericType(type, typeof(Task));
        var lambda = Expression.Lambda(lambdaType, call, param);

        var enqueueMethod = GetEnqueueMethod();
        var genericEnqueue = enqueueMethod.MakeGenericMethod(type);
        return (string)(
            genericEnqueue.Invoke(null, [client, lambda])
            ?? throw new InvalidOperationException("Failed to enqueue job")
        );
    }
}
