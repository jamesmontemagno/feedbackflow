#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

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
            var interactionService = context.ServiceProvider.GetRequiredService<IInteractionService>();
            var rcs = context.ServiceProvider.GetRequiredService<ResourceCommandService>();
            var logger =  context.ServiceProvider.GetRequiredService<ResourceLoggerService>().GetLogger(context.ResourceName);

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

            var result = await interactionService.PromptInputsAsync("Enter API Keys", "Enter the API keys ", inputs);

            if (!result.Canceled)
            {
                // Get the values
                openApiKey = result.Data[0].Value;
                openApiEndpoint = result.Data[1].Value;
                openApiModel = result.Data[2].Value == "other" ? result.Data[3].Value : result.Data[2].Value;

                logger.LogInformation("Set Endpoint = {Endpoint}", openApiEndpoint);
                logger.LogInformation("Set Model = {Model}", openApiModel);

                // Restart the resource after setting these values
                return await rcs.ExecuteCommandAsync(context.ResourceName, "resource-restart", context.CancellationToken);
            }

            return CommandResults.Success();
        }, 
        new CommandOptions());

builder.AddProject<Projects.feedbackwebapp>("feedback-webapp")
        .WithEnvironment("FeedbackApi__BaseUrl", feedbackFunctionsProject.GetEndpoint("http"))
        .WithEnvironment("FeedbackApi__UseMocks", "false")
        .WithEnvironment("FeedbackApp__AccessPassword", frontendPassword)
        ;

builder.Build().Run();
