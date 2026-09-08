using System.Windows;
using JobFinder.Desktop.ViewModels;
using JobFinder.DesktopServices.Clients;
using JobFinder.DesktopServices.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobFinder.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient<IJobFinderClient, JobFinderClient>(
                    client =>
                    {
                        var baseUrl = context.Configuration["ApiBaseUrl"];

                        if (string.IsNullOrWhiteSpace(baseUrl))
                        {
                            throw new InvalidOperationException(
                                "ApiBaseUrl is not configured.");
                        }

                        client.BaseAddress = new Uri(baseUrl);
                    });

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow =
            _host.Services.GetRequiredService<MainWindow>();

        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
