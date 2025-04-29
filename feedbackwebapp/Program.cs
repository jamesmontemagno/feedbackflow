using FeedbackWebApp.Components;
using FeedbackWebApp.Services;
using FeedbackWebApp.Services.Authentication;
using FeedbackWebApp.Services.ContentFeed;
using FeedbackWebApp.Services.Feedback;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add SpeechSynthesis services
builder.Services.AddSpeechSynthesisServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true)
    .AddHubOptions(options =>
    {
        options.EnableDetailedErrors = true;
        options.MaximumReceiveMessageSize = 102_400; // 100 KB or more
    });

builder.Services.AddHttpClient("DefaultClient")
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<FeedbackServiceProvider>();
builder.Services.AddScoped<ContentFeedServiceProvider>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddMemoryCache(); // Add this line for caching support

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
