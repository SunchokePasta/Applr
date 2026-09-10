using JobFinder.Services.APIs;
using JobFinder.Services.Interfaces;
using JobFinder.Services.Services;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add MVC controllers.
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

// Register application services.
builder.Services.AddScoped<IArbeitNowService, ArbeitNowService>();
builder.Services.AddScoped<ITrackrScraperService, TrackrScraperService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
