var builder = DistributedApplication.CreateBuilder(args);

var feedbackFunctionsProject = builder.AddAzureFunctionsProject<Projects.feedbackfunctions>("feedback-functions");

builder.AddProject<Projects.feedbackwebapp>("feedback-webapp")
        .WithEnvironment("FeedbackApi:BaseUrl", feedbackFunctionsProject.GetEndpoint("http"))
        .WithEnvironment("FeedbackApi:UseMocks", "false");

builder.Build().Run();
