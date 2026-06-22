using System.IO;
using System.Windows;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Infrastructure;
using FacturacionArca.Wpf.ViewModels;
using FacturacionArca.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FacturacionArca.Wpf;

public partial class App : System.Windows.Application
{
    public static IHost Host { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var carpetaDatos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FacturacionArca");
        Directory.CreateDirectory(carpetaDatos);
        var sqlitePath = Path.Combine(carpetaDatos, "facturacion.db");
        var logsPath = Path.Combine(carpetaDatos, "logs", "facturacion-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(logsPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddFacturacionArcaInfrastructure(sqlitePath);

                services.AddSingleton<MainViewModel>();
                services.AddTransient<ListaProformasViewModel>();
                services.AddTransient<DetalleProformaViewModel>();
                services.AddTransient<HistorialComprobantesViewModel>();
                services.AddTransient<ConfiguracionViewModel>();
                services.AddTransient<ExportacionesViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        await Host.StartAsync();
        await InfrastructureBootstrap.InicializarBaseAsync(Host.Services);

        var watcher = Host.Services.GetRequiredService<IProformaWatcher>();
        var configRepo = Host.Services.GetRequiredService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<IConfiguracionRepository>();
        var cfg = await configRepo.GetAsync();
        if (!string.IsNullOrWhiteSpace(cfg.CarpetaProformas) && Directory.Exists(cfg.CarpetaProformas))
            watcher.Iniciar(cfg.CarpetaProformas);

        var main = Host.Services.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (Host is not null)
            {
                Host.Services.GetService<IProformaWatcher>()?.Dispose();
                await Host.StopAsync(TimeSpan.FromSeconds(5));
                Host.Dispose();
            }
        }
        finally
        {
            Log.CloseAndFlush();
        }
        base.OnExit(e);
    }
}
