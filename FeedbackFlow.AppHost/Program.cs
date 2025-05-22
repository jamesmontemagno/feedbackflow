var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddAzureFunctionsProject<Projects.feedbackfunctions>("feedback-functions");

builder.AddProject<Projects.feedbackwebapp>("feedback-webapp")
        .WithEnvironment("FeedbackApi:BaseUrl", api.GetEndpoint("http"))
        .WithEnvironment("FeedbackApi:UseMocks", "false");

builder.Build().Run();
