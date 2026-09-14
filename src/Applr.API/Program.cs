using Applr.Services.APIs;
using Applr.Services.Interfaces;
using Applr.Services.Services;
using Refit;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Console + a rolling daily file under ./logs, anchored to the app's own
// content root rather than the process's working directory.
var logDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logDirectory);

builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logDirectory, "log-.txt"),
        rollingInterval: RollingInterval.Day));

builder.Services.AddControllers();

// Register the ArbeitNow Refit client.
builder.Services
    .AddRefitClient<IArbeitNowApi>()
    .ConfigureHttpClient(client =>
    {
        var baseUrl = builder.Configuration["JobBoardBaseUrl"]
            ?? throw new InvalidOperationException(
                "JobBoardBaseUrl is not configured.");

        client.BaseAddress = new Uri(baseUrl);
    });

// Register the Applr.RestApi (DB-backed jobs) Refit client.
builder.Services
    .AddRefitClient<ITrackrRestApi>()
    .ConfigureHttpClient(client =>
    {
        var baseUrl = builder.Configuration["TrackrApiBaseUrl"]
            ?? throw new InvalidOperationException(
                "TrackrApiBaseUrl is not configured.");

        client.BaseAddress = new Uri(baseUrl);
    });

// Register application services.
builder.Services.AddScoped<IArbeitNowService, ArbeitNowService>();
builder.Services.AddScoped<ITrackrService, TrackrService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
