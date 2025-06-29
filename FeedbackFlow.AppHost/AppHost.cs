using FeedbackFlow.AppHost;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

var defaultPW = new GenerateParameterDefault{MinLength = 8};
var frontendPassword = ParameterResourceBuilderExtensions.CreateGeneratedParameter(builder, "password", secret: true, defaultPW);
builder.AddResource(frontendPassword);

var storage = builder.AddAzureStorage("ff-storage")
        .RunAsEmulator( (ctx) =>
        {
            ctx.WithLifetime(ContainerLifetime.Persistent);
        });

var blobs = storage.AddBlobs("ff-blobs");
// Create the blob containers, these names matter.
blobs.AddBlobContainer("shared-analyses");
blobs.AddBlobContainer("reports");
blobs.AddBlobContainer("hackernews-cache");

var feedbackFunctionsProject = builder.AddAzureFunctionsProject<Projects.feedbackfunctions>("feedback-functions")
        .WithHostStorage(storage)
        .WithEnvironment(context =>
        {
            if(!KeyConfiguration.HasEnvFile(builder.AppHostDirectory))
            {
                return;
            }

            KeyConfiguration.ReadFromEnvFile(builder.AppHostDirectory);

            context.Logger.LogInformation("Use Mocks = {Mocks}", KeyConfiguration.Keys.UseMocks);
            context.Logger.LogInformation("Endpoint = {Endpoint}", KeyConfiguration.Keys.AzureOpenAIEndpoint);
            context.Logger.LogInformation("Model = {Model}", KeyConfiguration.Keys.AzureOpenAIModel);

            KeyConfiguration.WriteToConfiguration(context);
        })
        .WithCommand("api-keys", "Enter API Keys", async context => 
        {
            if (KeyConfiguration.HasEnvFile(builder.AppHostDirectory))
            {
                KeyConfiguration.ReadFromEnvFile(builder.AppHostDirectory);
            }
            await KeyConfiguration.PromptForValues(context.ResourceName, context.ServiceProvider, builder.AppHostDirectory, restart: true, context.CancellationToken);

            return CommandResults.Success();
        }, 
        new CommandOptions
        {
            IconName = "Key"
        });

bool promptForValues = true;

builder.Eventing.Subscribe<BeforeResourceStartedEvent>(feedbackFunctionsProject.Resource, async (e, ct) =>
{
    if (KeyConfiguration.HasEnvFile(builder.AppHostDirectory))
    {
        KeyConfiguration.ReadFromEnvFile(builder.AppHostDirectory);
        promptForValues = false;
    }

    if (promptForValues)
    {
        await KeyConfiguration.PromptForValues(e.Resource.Name, e.Services, builder.AppHostDirectory, restart: false, ct);
        promptForValues = false;
    }
});



builder.AddProject<Projects.feedbackwebapp>("feedback-webapp")
        .WithEnvironment("FeedbackApi__BaseUrl", feedbackFunctionsProject.GetEndpoint("http"))
        .WithEnvironment("FeedbackApi__UseMocks", "false")
        .WithEnvironment("FeedbackApp__AccessPassword", frontendPassword);

builder.Build().Run();
