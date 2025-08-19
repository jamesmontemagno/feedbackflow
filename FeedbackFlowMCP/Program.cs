﻿using Microsoft.Extensions.Hosting;
using FeedbackFlowMCP;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;

/*var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FeedbackFlowTools>();*/

var builder = WebApplication.CreateBuilder(args);

// Configure the port for Azure Functions custom handler
var port = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT") ?? "80";
builder.WebHost.UseUrls($"http://localhost:{port}");

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<FeedbackFlowTools>();

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapMcp();

app.Run();