var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureFunctionsProject<Projects.feedbackfunctions>("feedbackfunctions");

builder.AddProject<Projects.feedbackwebapp>("feedbackwebapp");

builder.Build().Run();
