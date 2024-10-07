using AttackOfTheLamas.Shared;
using AttackOfTheLamasWeb.Components;
using AttackOfTheLamasWeb.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddSingleton<IMessageHistory, MessageHistory>();
builder.Services.AddSingleton<IFileWatcherService, FileWatcherService>();
builder.Services.AddSingleton<IRealTimeMessageStreamer, RealTimeMessageStreamer>();
builder.Services.AddHttpClient<IGeminiApiService, GeminiApiService>((client, serviceProvider) =>
    {
        var configuration = builder.Configuration;
        var baseUrl = configuration["GeminiApi:BaseUrl"];
    
        client.BaseAddress = new Uri(baseUrl!);
        var messageStreamer = serviceProvider.GetRequiredService<IRealTimeMessageStreamer>();

        var apiKey = configuration["GeminiApi:ApiKey"];

        // GeminiApiService takes HttpClient, IMessageHistory, and the API Key
        return new GeminiApiService(client, apiKey!, messageStreamer);
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Ensure middleware order is correct
app.UseHttpsRedirection();   // HTTPS Redirection happens before routing

app.UseStaticFiles();        // Serves static files

app.UseRouting();            // Routing middleware (before UseEndpoints)

app.UseAntiforgery();        // Antiforgery for protecting against CSRF

app.UseEndpoints(endpoints =>
{
    // Map Razor Components within the endpoints
    endpoints.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
});

app.Run();