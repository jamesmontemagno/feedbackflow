using FeedbackWebApp.Services;
using FeedbackWebApp.Services.Account;
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

builder.Services.AddMemoryCache();

// Register ToastService and other services
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<IHistoryHelper, HistoryHelper>();

builder.Services.AddHttpClient("DefaultClient")
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<FeedbackServiceProvider>();
builder.Services.AddScoped<ContentFeedServiceProvider>();

// Register authentication service based on environment
var bypassAuth = builder.Configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
var isDevelopment = builder.Environment.IsDevelopment();

if (bypassAuth && isDevelopment)
{
    // Use debug authentication service for local development
    builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
    builder.Services.AddScoped<IAuthenticationHeaderService, AuthenticationHeaderService>();
}
else
{
    // Use OAuth authentication service for production/staging
    builder.Services.AddScoped<IAuthenticationService, ServerSideAuthService>();
    builder.Services.AddScoped<IAuthenticationHeaderService, ServerSideAuthenticationHeaderService>();
}

// Register registration error service
builder.Services.AddScoped<IRegistrationErrorService, RegistrationErrorService>();

// Register user management service
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Register authentication token refresh service


// Register the proper frontend account service that calls backend APIs
var useMocks = builder.Configuration.GetValue<bool>("UseMocks", false);
#if DEBUG
    useMocks = builder.Configuration.GetValue<bool>("UseMocks", true); // Default to mocks in debug
#endif

// Account Services using provider pattern
builder.Services.AddScoped<IAccountServiceProvider, AccountServiceProvider>();
builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddScoped<IReportServiceProvider, ReportServiceProvider>();
builder.Services.AddScoped<IReportRequestService, ReportRequestService>();
builder.Services.AddScoped<FeedbackWebApp.Services.IAdminReportConfigService>(serviceProvider =>
{
    if (useMocks)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<FeedbackWebApp.Services.Mock.MockAdminReportConfigService>>();
        return new FeedbackWebApp.Services.Mock.MockAdminReportConfigService(logger);
    }
    else
    {
        var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("DefaultClient");
        var headerService = serviceProvider.GetRequiredService<IAuthenticationHeaderService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AdminReportConfigService>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return new AdminReportConfigService(httpClient, headerService, logger, configuration);
    }
});

// Register Admin Dashboard Service
builder.Services.AddScoped<IAdminDashboardService>(serviceProvider =>
{
    if (useMocks)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<FeedbackWebApp.Services.Mock.MockAdminDashboardService>>();
        return new FeedbackWebApp.Services.Mock.MockAdminDashboardService(logger);
    }
    else
    {
        var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("DefaultClient");
        var headerService = serviceProvider.GetRequiredService<IAuthenticationHeaderService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AdminDashboardService>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return new AdminDashboardService(httpClient, headerService, logger, configuration);
    }
});

// Register Admin API Key Service
builder.Services.AddScoped<IAdminApiKeyService>(serviceProvider =>
{
    if (useMocks)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<MockAdminApiKeyService>>();
        return new MockAdminApiKeyService(logger);
    }
    else
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var headerService = serviceProvider.GetRequiredService<IAuthenticationHeaderService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AdminApiKeyService>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return new AdminApiKeyService(httpClientFactory, headerService, logger, configuration);
    }
});

// Register Admin User Tier Service
builder.Services.AddScoped<IAdminUserTierService>(serviceProvider =>
{
    if (useMocks)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<MockAdminUserTierService>>();
        return new MockAdminUserTierService(logger);
    }
    else
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var headerService = serviceProvider.GetRequiredService<IAuthenticationHeaderService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AdminUserTierService>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return new AdminUserTierService(httpClientFactory, headerService, logger, configuration);
    }
});
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddScoped<ISharedHistoryServiceProvider, SharedHistoryServiceProvider>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ITwitterAccessService, TwitterAccessService>();

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
// Conditionally expose robots.txt only for non-production environments.
// In Production we want to allow crawling (so we serve nothing / 404 which permits crawling by default).
// In Staging or Development we return a disallow-all robots file.
if (!app.Environment.IsProduction())
{
    app.MapGet("/robots.txt", async context =>
    {
        context.Response.ContentType = "text/plain";
        // File content maintained in wwwroot/robots.staging.txt
        await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "robots.staging.txt"));
    });
}
app.MapRazorComponents<FeedbackWebApp.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
