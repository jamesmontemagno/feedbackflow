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
var port = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddMcpServer()
    .WithHttpTransport((options)=>
    {
        options.Stateless = true;
    })
    .WithTools<FeedbackFlowTools>();

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapMcp(pattern: "/mcp");

app.Run();