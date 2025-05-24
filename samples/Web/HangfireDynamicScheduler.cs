namespace Web;

using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Hangfire;

public class HangfireDynamicScheduler(IBackgroundJobClient client)
{
    public string EnqueueByTypeName(string typeName, string methodName)
    {
        // Find the type in the current assembly
        var type =
            Assembly.GetExecutingAssembly().GetType(typeName)
            ?? throw new InvalidOperationException($"Type '{typeName}' not found.");

        // Find the method (must be instance, parameterless, returns Task)
        var method =
            type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' not found on type '{typeName}'."
            );

        // Create an instance of the type
        var instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);

        // Build Expression<Func<T, Task>> for Hangfire
        var param = Expression.Parameter(type, "x");
        var call = Expression.Call(param, method);

        Type lambdaType;
        if (method.ReturnType == typeof(Task))
        {
            lambdaType = typeof(Func<,>).MakeGenericType(type, typeof(Task));
        }
        else if (method.ReturnType == typeof(void))
        {
            lambdaType = typeof(Action<>).MakeGenericType(type);
        }
        else
        {
            throw new InvalidOperationException("Method must return void or Task.");
        }

        var lambda = Expression.Lambda(lambdaType, call, param);

        // Use Hangfire's generic Enqueue<T>
        var enqueueMethod = typeof(BackgroundJobClientExtensions)
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

        var genericEnqueue = enqueueMethod.MakeGenericMethod(type);

        // Call Enqueue<T>(client, Expression<Func<T, Task>>)
        return (string)genericEnqueue.Invoke(null, [client, lambda]);
    }
}
