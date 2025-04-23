using FeedbackWebApp.Components;
using FeedbackWebApp.Components.Feedback.Services;
using FeedbackWebApp.Components.ContentFeed.Services;
using FeedbackWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("DefaultClient")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(3); // Adjust timeout value as needed
    });
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
