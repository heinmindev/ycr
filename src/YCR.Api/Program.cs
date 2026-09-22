using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using YCR.Api.Common;
using YCR.Api.Common.Authentication;
using YCR.Api.Common.Authorization;
using YCR.Api.Endpoints.Health;
using YCR.Api.Endpoints.Network;
using YCR.Application;
using YCR.Application.Common.Abstractions;
using YCR.Infrastructure;
using YCR.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// The application credential, never the migrator's. There is no migration at startup: spec E7
// requires migrations to run as a separate step under a credential with DDL rights, and this
// process has none (S22).
var connectionString = builder.Configuration.GetConnectionString("Application")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Application is not configured. It must name the least-privilege "
        + "ycr_app credential and carry 'Current Language=us_english' (see the translator's "
        + "REQUIRED CONTROL and .env.example).");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ADR-0020: F-001 registers the authorization pipeline but no authentication handler. Outside
// Testing there is deliberately no scheme, so every endpoint requiring a caller answers 401
// until the ADR-0016 token feature lands. AuthenticationSchemeGuard proves that stays true.
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
// Turns a challenge into a plain 401 while ADR-0020 leaves the deployment with no scheme to
// challenge with; without it an unauthenticated request is a 500. See the type's remarks.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationResultHandler>();

builder.Services.AddYcrProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<YcrDbContext>("database", tags: ["ready"]);

var app = builder.Build();

await app.GuardAuthenticationSchemesAsync();

app.UseYcrExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

// The OpenAPI document, served at /openapi/v1.json. Development only: the document describes
// every route and permission, which is a map of the system that a deployed instance has no
// reason to hand out.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGroup("/api/v1").MapStationEndpoints();
app.MapHealthEndpoints();

await app.RunAsync();

/// <summary>
/// Named so <c>WebApplicationFactory&lt;Program&gt;</c> can reach it from <c>YCR.Api.Tests</c>.
/// </summary>
/// <remarks>
/// The test authentication handler is registered only through <c>ConfigureTestServices</c> in
/// that project and exists nowhere in <c>src/</c> (ADR-0020 items 2 and 4, spec S21a).
/// </remarks>
public partial class Program;
