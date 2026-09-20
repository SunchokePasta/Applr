using Applr.API.Middleware;
using Applr.Services.APIs;
using Applr.Services.Interfaces;
using Applr.Services.Options;
using Applr.Services.Services;
using Refit;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var logDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logDirectory);

const string LogTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()

    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)

    // Keep HttpClient's own per-request lines: when the REST API is
    // unreachable this is where "Connection refused" is spelled out.
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Information)

    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: LogTemplate)
    .WriteTo.File(
        Path.Combine(logDirectory, "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: LogTemplate,
        shared: true));

builder.Services.AddControllers();

var timeoutOptions = builder.Configuration
    .GetSection(TimeoutOptions.SectionName)
    .Get<TimeoutOptions>() ?? new TimeoutOptions();

builder.Services
    .AddRefitClient<IArbeitNowApi>()
    .ConfigureHttpClient(client =>
    {
        var baseUrl = builder.Configuration["JobBoardBaseUrl"]
            ?? throw new InvalidOperationException(
                "JobBoardBaseUrl is not configured.");

        client.BaseAddress = new Uri(baseUrl);

        client.Timeout = TimeSpan.FromSeconds(timeoutOptions.ArbeitNowSeconds);
    });

builder.Services
    .AddRefitClient<ITrackrRestApi>()
    .ConfigureHttpClient(client =>
    {
        var baseUrl = builder.Configuration["TrackrApiBaseUrl"]
            ?? throw new InvalidOperationException(
                "TrackrApiBaseUrl is not configured.");

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(timeoutOptions.TrackrSeconds);
    });

builder.Services
    .AddRefitClient<IGmailApi>()
    .ConfigureHttpClient(client =>
    {
        var baseUrl = builder.Configuration["GmailApiBaseUrl"]
            ?? throw new InvalidOperationException(
                "GmailApiBaseUrl is not configured.");

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(timeoutOptions.GmailSeconds);
    });

builder.Services.AddScoped<IArbeitNowService, ArbeitNowService>();
builder.Services.AddScoped<ITrackrService, TrackrService>();
builder.Services.AddScoped<IGmailService, GmailService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
