namespace YCR.Worker;

/// <summary>
/// The worker process (<c>docs/06</c>). F-002 gives it one command, <c>bootstrap-administrator</c>
/// (plan P8), which runs without starting the host. With no command it is still the bare host
/// F-001 created.
/// </summary>
/// <remarks>
/// An explicit class in <c>YCR.Worker</c> rather than top-level statements, so it is not a second
/// global <c>Program</c> beside the API's in test projects that reference both.
/// </remarks>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (BootstrapAdministratorCli.Matches(args))
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            await using var services = new ServiceCollection()
                .AddLogging(logging => logging
                    .AddConfiguration(configuration.GetSection("Logging"))
                    .AddSimpleConsole(console => console.SingleLine = true))
                .AddBootstrapAdministrator(configuration, environment)
                .BuildServiceProvider();

            return await BootstrapAdministratorCli.RunAsync(
                args,
                BootstrapAdministratorCli.PasswordReader(),
                Console.Out,
                Console.Error,
                services);
        }

        var builder = Host.CreateApplicationBuilder(args);
        var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
