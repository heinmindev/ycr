// Composition root. Step 1 places a minimal host here so the solution builds;
// the real pipeline (ProblemDetails, authorization, endpoints) arrives at step 11.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

await app.RunAsync();

/// <summary>
/// Exposed so <c>WebApplicationFactory</c> in YCR.Api.Tests can reach the host (ADR-0020 item 2).
/// </summary>
public partial class Program;
