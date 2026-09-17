using Applr.API.Middleware;
using Applr.Services.APIs;
using Applr.Services.Interfaces;
using Applr.Services.Services;
using Refit;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Console + a rolling daily file under ./logs, anchored to the app's own
// content root rather than the process's working directory.
var logDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logDirectory);

// SourceContext is what tells you whether a line came from a controller,
// a service, the Refit client or the middleware. Without it every line
// looks the same.
const string LogTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()

    // ASP.NET writes four lines per request ("Request starting",
    // "Executing endpoint", "Route matched", "Request finished").
    // UseSerilogRequestLogging below replaces all four with one summary
    // line that includes the elapsed time, so the originals are noise.
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

// Register the ArbeitNow Refit client.
builder.Services
    .AddRefitClient<IArbeitNowApi>()
    .ConfigureHttpClient(client =>
    {
        var baseUrl = builder.Configuration["JobBoardBaseUrl"]
            ?? throw new InvalidOperationException(
                "JobBoardBaseUrl is not configured.");

        client.BaseAddress = new Uri(baseUrl);

        // Shorter than HttpClient's 100s default. A hung upstream should
        // become a 504 the user can see while they still care, not a
        // spinner that outlives their patience.
        client.Timeout = TimeSpan.FromSeconds(30);
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
        client.Timeout = TimeSpan.FromSeconds(30);
    });

// Register application services.
builder.Services.AddScoped<IArbeitNowService, ArbeitNowService>();
builder.Services.AddScoped<ITrackrService, TrackrService>();

var app = builder.Build();

// First in the pipeline on purpose: anything registered after this is
// wrapped by it, so a throw anywhere downstream -- a controller, a
// service, a Refit call into Applr.RestApi -- becomes a logged
// ApiErrorResponse the desktop app can show, instead of an unhandled
// 500 with an empty body.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// One line per request: method, path, status, elapsed ms. Replaces the
// four ASP.NET lines suppressed above.
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
