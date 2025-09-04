﻿using Microsoft.Extensions.Hosting;
using FeedbackFlowMCP;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FeedbackFlowTools>();

builder.Services.AddHttpClient();


await builder.Build().RunAsync();