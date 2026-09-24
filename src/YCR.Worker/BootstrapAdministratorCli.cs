using Microsoft.Extensions.DependencyInjection.Extensions;
using YCR.Application;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity;
using YCR.Application.Identity.BootstrapAdministrator;
using YCR.Infrastructure;

namespace YCR.Worker;

/// <summary>
/// <c>dotnet YCR.Worker.dll bootstrap-administrator --username &lt;name&gt;</c>: creates the first
/// <c>SystemAdministrator</c> with a must-change password, once (D10; plan P8; spec S32, S32a).
/// </summary>
/// <remarks>
/// <para>
/// The initial password is read from <b>standard input</b> — never from the command line, where
/// process listings and shell history would keep it, and never from an environment variable
/// (R13). A <c>--password</c> argument is refused outright. The command prints nothing secret.
/// </para>
/// <para>
/// Exit codes: <c>0</c> created; <c>1</c> refused by a rule (an administrator already exists, the
/// username or password is invalid, or the environment is Production, where ADR-0023 item 4 as
/// amended refuses a privileged account until MFA ships) — nothing is created or written;
/// <c>2</c> usage error.
/// </para>
/// <para>
/// The host is never started: the command composes <c>AddInfrastructure</c> and
/// <c>AddApplication</c> over <c>ConnectionStrings:Application</c> (the least-privilege
/// <c>ycr_app</c> credential, which needs only <c>INSERT</c> on <c>identity.Users</c>,
/// <c>identity.UserRoles</c> and the ledger) with <see cref="SystemCurrentUser"/>, runs the one
/// handler, and exits.
/// </para>
/// </remarks>
public static class BootstrapAdministratorCli
{
    public const string CommandName = "bootstrap-administrator";

    public const int Created = 0;
    public const int Refused = 1;
    public const int UsageError = 2;

    private const string Usage =
        "Usage: YCR.Worker bootstrap-administrator --username <name>   (the password is read from standard input)";

    public static bool Matches(IReadOnlyList<string> args) =>
        args is { Count: > 0 } && string.Equals(args[0], CommandName, StringComparison.Ordinal);

    /// <summary>The services the command needs, and nothing that serves requests.</summary>
    /// <param name="environmentName">
    /// The worker's environment (<c>DOTNET_ENVIRONMENT</c>, <c>Production</c> when unset). In
    /// Production the command is refused with <c>Identity.PrivilegedRoleRequiresMfa</c> until MFA
    /// ships (ADR-0023 item 4 as amended; S-1).
    /// </param>
    public static IServiceCollection AddBootstrapAdministrator(this IServiceCollection services, IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environmentName);
        var connectionString = configuration.GetConnectionString("Application")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Application is not configured. It must name the least-privilege ycr_app credential.");

        services.AddInfrastructure(connectionString);
        services.AddSingleton(PrivilegedRoleGate.ForEnvironment(environmentName));
        services.AddApplication();
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        return services;
    }

    /// <param name="args">The full argument list, starting with <see cref="CommandName"/>.</param>
    /// <param name="stdin">Where the password is read: its first line.</param>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(services);

        if (args.Skip(1).Any(argument =>
                argument.StartsWith("--password", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-p", StringComparison.Ordinal)))
        {
            await stderr.WriteLineAsync("Refused: a password is never accepted on the command line. Pipe it to standard input.");
            return UsageError;
        }

        if (args.Count != 3
            || !string.Equals(args[1], "--username", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(args[2]))
        {
            await stderr.WriteLineAsync(Usage);
            return UsageError;
        }

        var password = await stdin.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(password))
        {
            await stderr.WriteLineAsync("No password was read from standard input.");
            await stderr.WriteLineAsync(Usage);
            return UsageError;
        }

        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<BootstrapAdministratorHandler>();
        var result = await handler.Handle(new BootstrapAdministratorCommand(args[2], password), cancellationToken);

        if (result.IsFailure)
        {
            await stderr.WriteLineAsync($"Refused ({result.Error.Code}): {result.Error.Message} Nothing was created.");
            return Refused;
        }

        await stdout.WriteLineAsync(
            $"Created the SystemAdministrator account {result.Value}. The password must be changed at the first sign-in.");
        return Created;
    }

    /// <summary>
    /// Standard input when it is redirected (a pipe or a file); otherwise one line read from the
    /// terminal without echoing it.
    /// </summary>
    public static TextReader PasswordReader()
    {
        if (Console.IsInputRedirected)
        {
            return Console.In;
        }

        Console.Error.Write("Initial password: ");
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }

                continue;
            }

            buffer.Append(key.KeyChar);
        }

        Console.Error.WriteLine();
        return new StringReader(buffer.ToString());
    }
}
