using FeedbackWebApp.Services;
using FeedbackWebApp.Services.Authentication;
using FeedbackWebApp.Services.ContentFeed;
using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using Microsoft.AspNetCore.Rewrite;
using SharedDump.Services;
using SharedDump.Services.Interfaces;

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
        options.MaximumReceiveMessageSize = 1_024_000; // 200 KB or more
    });

// Add HttpContextAccessor for server-side authentication
builder.Services.AddHttpContextAccessor();

// Add Data Protection services for secure authentication
builder.Services.AddDataProtection();
    
// Register ToastService and other services
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<IHistoryHelper, HistoryHelper>();

builder.Services.AddHttpClient("DefaultClient")
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<FeedbackServiceProvider>();
builder.Services.AddScoped<ContentFeedServiceProvider>();

// Register authentication service based on configuration
var useEasyAuth = builder.Configuration.GetValue<bool>("Authentication:UseEasyAuth", false);
if (useEasyAuth)
{
    // Use server-side authentication service for better security
    builder.Services.AddScoped<IAuthenticationService, ServerSideAuthService>();
    builder.Services.AddScoped<IAuthenticationHeaderService, ServerSideAuthenticationHeaderService>();
}
else
{
    // Fallback to old authentication service
    builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
    builder.Services.AddScoped<IAuthenticationHeaderService, AuthenticationHeaderService>();
}

// Register user management service
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Keep the old AuthenticationService for backward compatibility
//builder.Services.AddScoped<AuthenticationService>();

builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddScoped<IReportServiceProvider, ReportServiceProvider>();
builder.Services.AddScoped<IReportRequestService, ReportRequestService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddScoped<IAnalysisSharingService, AnalysisSharingService>();
builder.Services.AddScoped<IExportService, ExportService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    
    
    if (!app.Environment.IsStaging())
    {
        app.UseRewriter(new RewriteOptions().AddRedirectToWwwPermanent());
    }
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<FeedbackWebApp.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
