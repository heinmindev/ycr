// Listed by docs/06 so the project exists from the start. F-001 gives it no work
// to do (spec section 9); it is a bare host until a feature needs one.

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();

await host.RunAsync();
