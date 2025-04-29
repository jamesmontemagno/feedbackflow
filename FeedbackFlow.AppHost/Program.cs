var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureFunctionsProject<Projects.feedbackfunctions>("feedback-functions");

builder.AddProject<Projects.feedbackwebapp>("feedback-webapp");

builder.Build().Run();
