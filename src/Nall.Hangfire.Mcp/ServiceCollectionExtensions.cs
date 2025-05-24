namespace Nall.Hangfire.Mcp;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHangfireMcp(this IServiceCollection services)
    {
        services.AddSingleton<IHangfireDynamicScheduler, HangfireDynamicScheduler>();

        return services;
    }
}
