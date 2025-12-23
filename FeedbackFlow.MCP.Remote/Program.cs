using Microsoft.Extensions.Hosting;
using FeedbackFlow.MCP.Shared;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

/*var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FeedbackFlowTools>();*/

var builder = WebApplication.CreateBuilder(args);

// Configure the port for Azure Functions custom handler
var port = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddMcpServer()
    .WithHttpTransport((options)=>
    {
        options.Stateless = true;
    })
    .WithTools<FeedbackFlowToolsShared>();

builder.Services.AddHttpClient();
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthenticationProvider, RemoteAuthenticationProvider>();

var app = builder.Build();

app.MapMcp(pattern: "/mcp");

app.Run();