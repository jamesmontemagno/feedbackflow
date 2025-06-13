using FeedbackWebApp.Components;
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
    
// Register ToastService and other services
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<IHistoryHelper, HistoryHelper>();

builder.Services.AddHttpClient("DefaultClient")
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<FeedbackServiceProvider>();
builder.Services.AddScoped<ContentFeedServiceProvider>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddScoped<IReportServiceProvider, ReportServiceProvider>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddScoped<ICommentsService, CommentsService>();
builder.Services.AddScoped<IExportDataService, ExportDataService>();
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
