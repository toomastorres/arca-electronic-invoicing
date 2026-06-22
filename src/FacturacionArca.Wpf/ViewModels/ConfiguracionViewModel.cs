using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Application.UseCases;
using FacturacionArca.Domain.Configuracion;
using Microsoft.Extensions.DependencyInjection;

namespace FacturacionArca.Wpf.ViewModels;

public partial class ConfiguracionViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPrinterService _printer;
    private readonly IProformaWatcher _watcher;

    [ObservableProperty] private ConfiguracionEmpresa configuracion = new();
    [ObservableProperty] private string? mensaje;
    public IReadOnlyList<string> Impresoras { get; }
    public IReadOnlyList<ModoOperacion> Modos { get; } = new[] { ModoOperacion.Homologacion, ModoOperacion.Produccion };

    public ConfiguracionViewModel(IServiceScopeFactory scopeFactory, IPrinterService printer, IProformaWatcher watcher)
    {
        _scopeFactory = scopeFactory;
        _printer = printer;
        _watcher = watcher;
        Impresoras = printer.ListarImpresoras();
        _ = CargarAsync();
    }

    public async Task CargarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IConfiguracionRepository>();
        Configuracion = await repo.GetAsync();
    }

    [RelayCommand]
    public async Task GuardarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IConfiguracionRepository>();
        await repo.SaveAsync(Configuracion);

        if (!string.IsNullOrWhiteSpace(Configuracion.CarpetaProformas) && Directory.Exists(Configuracion.CarpetaProformas))
            _watcher.Iniciar(Configuracion.CarpetaProformas);

        Mensaje = "Configuración guardada.";
    }

    [RelayCommand]
    public async Task SincronizarMaestrosAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<SincronizarMaestrosArca>();
            var m = await sync.EjecutarAsync();
            Mensaje = $"Sincronizados {m.TiposComprobante.Count} tipos de cbte, {m.TiposIva.Count} alícuotas IVA, {m.TiposMoneda.Count} monedas.";
        }
        catch (Exception ex)
        {
            Mensaje = $"Error: {ex.Message}";
        }
    }
}
