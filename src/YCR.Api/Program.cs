using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;
using YCR.Api.Common;
using YCR.Api.Common.Authentication;
using YCR.Api.Common.Authorization;
using YCR.Api.Endpoints.Health;
using YCR.Api.Endpoints.Identity;
using YCR.Api.Endpoints.Network;
using YCR.Application;
using YCR.Application.Common.Abstractions;
using YCR.Application.Identity;
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

// F-002 (ADR-0016, ADR-0023): the framework JwtBearerHandler is the one scheme; the principal is
// rebuilt from the database on every (uncached) request.
builder.Services.AddYcrAuthentication(builder.Configuration);

// ADR-0023 item 4 as amended (S-1): privileged roles cannot be granted in Production until MFA
// ships. Before AddApplication, whose fallback gate blocks.
builder.Services.AddSingleton(PrivilegedRoleGate.ForEnvironment(builder.Environment.EnvironmentName));
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddYcrProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<YcrDbContext>("database", tags: ["ready"]);

var app = builder.Build();

await app.GuardAuthenticationSchemesAsync();
app.ValidateSigningKeys();

// First, so every response — including the exception handler's and the challenge's — carries
// the security headers (R24, plan P10). No CORS anywhere (D16).
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseYcrExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
// R26: after authentication (the server-built principal) and before authorization (N1).
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseAuthorization();

// The OpenAPI document, served at /openapi/v1.json, and the Scalar API reference UI that renders
// it, served at /scalar. Development only: the document describes
// every route and permission, which is a map of the system that a deployed instance has no
// reason to hand out.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();
api.MapUserEndpoints();
api.MapStationEndpoints();
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
