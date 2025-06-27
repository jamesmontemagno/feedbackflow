#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

var defaultPW = new GenerateParameterDefault{MinLength = 8};
var frontendPassword = ParameterResourceBuilderExtensions.CreateGeneratedParameter(builder, "password", secret: true, defaultPW);
builder.AddResource(frontendPassword);

var storage = builder.AddAzureStorage("ff-storage")
        .RunAsEmulator( (ctx) =>
        {
            ctx.WithLifetime(ContainerLifetime.Persistent);
        })
        ;

var blobs = storage.AddBlobs("ff-blobs");
// Create the blob containers, these names matter.
blobs.AddBlobContainer("shared-analyses");
blobs.AddBlobContainer("reports");
blobs.AddBlobContainer("hackernews-cache");


 // Get the values for Azure Open AI
string? openApiKey = null;
string? openApiEndpoint = null;
string? openApiModel = null;

var feedbackFunctionsProject = builder.AddAzureFunctionsProject<Projects.feedbackfunctions>("feedback-functions")
        .WithHostStorage(storage)
        .WithEnvironment(context =>
        {
            var useMocks = string.IsNullOrWhiteSpace(openApiEndpoint) || 
                           string.IsNullOrWhiteSpace(openApiKey) || 
                           string.IsNullOrWhiteSpace(openApiModel);

            context.EnvironmentVariables["UseMocks"] = useMocks.ToString();

            context.Logger.LogInformation("Use Mocks = {Mocks}", useMocks);
            context.Logger.LogInformation("Endpoint = {Endpoint}", openApiEndpoint);
            context.Logger.LogInformation("Model = {Model}", openApiModel);

            if (!useMocks)
            {
                context.EnvironmentVariables["Azure__OpenAI__ApiKey"] = openApiKey!;
                context.EnvironmentVariables["Azure__OpenAI__Endpoint"] = openApiEndpoint!;
                context.EnvironmentVariables["Azure__OpenAI__Deployment"] = openApiModel!;
            }
        })
        .WithCommand("api-keys", "Enter API Keys", async context => 
        {
            await PromptForValues(context.ResourceName, context.ServiceProvider, restart: true, context.CancellationToken);

            return CommandResults.Success();
        }, 
        new CommandOptions
        {
            IconName = "FoodPizzaFilled"
        });

bool promptForValues = true;

builder.Eventing.Subscribe<BeforeResourceStartedEvent>(feedbackFunctionsProject.Resource, async (e, ct) =>
{
    // Read the values from the .env file if they exist and don't prompt the user
    var envFilePath = Path.Combine(builder.AppHostDirectory, ".env");

    if (File.Exists(envFilePath))
    {
        var envLines = File.ReadAllLines(envFilePath);
        foreach (var line in envLines)
        {
            if (line.StartsWith("Azure__OpenAI__ApiKey="))
            {
                openApiKey = line.Split('=')[1].Trim();
            }
            else if (line.StartsWith("Azure__OpenAI__Endpoint="))
            {
                openApiEndpoint = line.Split('=')[1].Trim();
            }
            else if (line.StartsWith("Azure__OpenAI__Deployment="))
            {
                openApiModel = line.Split('=')[1].Trim();
            }
        }
        
        promptForValues = false;
    }

    if (promptForValues)
    {
        await PromptForValues(e.Resource.Name, e.Services, restart: false, ct);
        promptForValues = false;
    }
});

async Task PromptForValues(
    string resourceName,
    IServiceProvider provider, 
    bool restart,
    CancellationToken cancellationToken)
{
    var interactionService = provider.GetRequiredService<IInteractionService>();
    var rcs = provider.GetRequiredService<ResourceCommandService>();
    var logger =  provider.GetRequiredService<ResourceLoggerService>().GetLogger(resourceName);

    var inputs = new InteractionInput []
    {
        new() { InputType = InputType.SecretText, Label = "Azure Open AI Key", Placeholder = "Enter Azure Open AI Key" },
        new() { InputType = InputType.Text, Label = "Azure Open AI Endpoint", Placeholder = "Enter Azure Open AI Endpoint" },
        new() { InputType = InputType.Choice, Label = "Azure Open AI Model", Placeholder = "Select an AI model", 
        Options = [
            KeyValuePair.Create("gpt-4.1", "GPT 4.1"),
            KeyValuePair.Create("gpt-4.1-mini", "GPT 4.1 Mini"),
            KeyValuePair.Create("gpt-4.1-nano", "GPT 4.1 Nano"),
            KeyValuePair.Create("other", "Other"),
        ] },
        new() { InputType = InputType.Text, Label = "Azure Open AI Other Model", Placeholder = "Enter Azure Open AI Model" },
    };

    var result = await interactionService.PromptInputsAsync("Enter API Keys", "Enter the API keys ", inputs, cancellationToken: cancellationToken);

    if (!result.Canceled)
    {
        // Get the values
        openApiKey = result.Data[0].Value;
        openApiEndpoint = result.Data[1].Value;
        openApiModel = result.Data[2].Value == "other" ? result.Data[3].Value : result.Data[2].Value;

        logger.LogInformation("Set Endpoint = {Endpoint}", openApiEndpoint);
        logger.LogInformation("Set Model = {Model}", openApiModel);

        // Store the values in an .env file
        File.WriteAllText(
            Path.Combine(builder.AppHostDirectory, ".env"),
            $"""
            Azure__OpenAI__ApiKey={openApiKey}
            Azure__OpenAI__Endpoint={openApiEndpoint}
            Azure__OpenAI__Deployment={openApiModel}
            """
        );

        if (restart)
        {
            // Restart the resource after setting these values
            await rcs.ExecuteCommandAsync(resourceName, "resource-restart", cancellationToken);
        }
    }
    else
    {
        // Clear the values
        openApiKey = null;
        openApiEndpoint = null;
        openApiModel = null;
    }
}

builder.AddProject<Projects.feedbackwebapp>("feedback-webapp")
        .WithEnvironment("FeedbackApi__BaseUrl", feedbackFunctionsProject.GetEndpoint("http"))
        .WithEnvironment("FeedbackApi__UseMocks", "false")
        .WithEnvironment("FeedbackApp__AccessPassword", frontendPassword)
        ;

builder.Build().Run();
