namespace HangfireMCP;

using System.Globalization;
using Hangfire;
using Hangfire.PostgreSql;
using HangfireMCP;

public static class HangfireExtensions
{
    public static void AddHangfire(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var defaultCulture = CultureInfo.InvariantCulture;
        GlobalConfiguration.Configuration.UseDefaultCulture(
            culture: defaultCulture,
            uiCulture: defaultCulture,
            captureDefault: false
        );

        var connectionString =
            Environment.GetEnvironmentVariable("HANGFIRE_CONNECTION_STRING")
            ?? builder.Configuration.GetConnectionString("hangfire");

        builder.Services.AddHangfire(globalConfiguration =>
            globalConfiguration.UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(connectionString)
            )
        );
    }
}
