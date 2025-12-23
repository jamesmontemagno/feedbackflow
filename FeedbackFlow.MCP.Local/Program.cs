using Microsoft.Extensions.Hosting;
using FeedbackFlow.MCP.Shared;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FeedbackFlowToolsShared>();

builder.Services.AddHttpClient();
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10));
});
builder.Services.AddScoped<IAuthenticationProvider, LocalAuthenticationProvider>();

await builder.Build().RunAsync();