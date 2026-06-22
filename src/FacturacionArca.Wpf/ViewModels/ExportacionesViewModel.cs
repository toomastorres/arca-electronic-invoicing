using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FacturacionArca.Wpf.ViewModels;

public partial class ExportacionesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExportacionesViewModel> _logger;

    [ObservableProperty] private DateTime fechaDesde = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime fechaHasta = DateTime.Today;
    [ObservableProperty] private string? mensaje;
    [ObservableProperty] private bool trabajando;

    [ObservableProperty] private string padronArchivoSeleccionado = "";
    [ObservableProperty] private int padronCargados;
    [ObservableProperty] private DateTime? padronUltimaPublicacion;
    [ObservableProperty] private double padronProgreso;

    public ExportacionesViewModel(IServiceScopeFactory sf, ILogger<ExportacionesViewModel> logger)
    {
        _scopeFactory = sf;
        _logger = logger;
        _ = ActualizarStatsPadronAsync();
    }

    [RelayCommand]
    public async Task ExportarLibroIvaVentasAsync()
    {
        try
        {
            Trabajando = true;
            var dlg = new SaveFileDialog
            {
                Filter = "CSV (Excel) (*.csv)|*.csv|Todos|*.*",
                FileName = $"LibroIvaVentas_{FechaDesde:yyyy-MM}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<ExportarLibroIvaVentas>();
            var r = await uc.EjecutarAsync(DateOnly.FromDateTime(FechaDesde), DateOnly.FromDateTime(FechaHasta), dlg.FileName);
            Mensaje = $"Libro IVA generado: {r.Cantidad} comprobantes. Total ${r.TotalGeneral:N2}. Archivo: {r.RutaArchivo}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export Libro IVA");
            Mensaje = $"Error: {ex.Message}";
        }
        finally { Trabajando = false; }
    }

    [RelayCommand]
    public async Task ExportarAgipPercepcionesAsync()
    {
        try
        {
            Trabajando = true;
            var dlg = new SaveFileDialog
            {
                Filter = "TXT (*.txt)|*.txt",
                FileName = $"AGIP_Percepciones_{FechaDesde:yyyyMM}.txt"
            };
            if (dlg.ShowDialog() != true) return;
            var carpeta = Path.GetDirectoryName(dlg.FileName) ?? ".";
            var rutaFact = dlg.FileName;
            var rutaNc = Path.Combine(carpeta, $"AGIP_NotasCredito_{FechaDesde:yyyyMM}.txt");

            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<ExportarAgipPercepcionesTxt>();
            var r = await uc.EjecutarAsync(DateOnly.FromDateTime(FechaDesde), DateOnly.FromDateTime(FechaHasta), rutaFact, rutaNc);
            Mensaje = $"AGIP exportado: {r.CantidadFacturas} facturas, {r.CantidadNotasCredito} NC.\nFacturas: {r.RutaFacturas}\nNC: {r.RutaNotasCredito}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export AGIP");
            Mensaje = $"Error: {ex.Message}";
        }
        finally { Trabajando = false; }
    }

    [RelayCommand]
    public void SeleccionarPadron()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "AGIP Padrón (*.TXT)|*.TXT|Todos|*.*",
            Title = "Seleccione el archivo de padrón AGIP IIBB"
        };
        if (dlg.ShowDialog() == true)
            PadronArchivoSeleccionado = dlg.FileName;
    }

    [RelayCommand]
    public async Task ImportarPadronAsync()
    {
        if (string.IsNullOrWhiteSpace(PadronArchivoSeleccionado) || !File.Exists(PadronArchivoSeleccionado))
        {
            Mensaje = "Seleccione primero un archivo de padrón.";
            return;
        }

        try
        {
            Trabajando = true;
            PadronProgreso = 0;
            Mensaje = "Importando padrón AGIP (esto puede tardar varios minutos para padrones grandes)...";

            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<ImportarPadronAgip>();
            var progress = new Progress<int>(n => PadronProgreso = n);
            var r = await uc.EjecutarAsync(PadronArchivoSeleccionado, progress);
            if (r.Error is not null)
            {
                Mensaje = $"Error: {r.Error}";
                return;
            }
            Mensaje = $"Padrón importado: {r.CantidadCargada:N0} entradas (publicación {r.FechaPublicacion:dd/MM/yyyy}).";
            await ActualizarStatsPadronAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import padrón AGIP");
            Mensaje = $"Error: {ex.Message}";
        }
        finally { Trabajando = false; }
    }

    private async Task ActualizarStatsPadronAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPadronIibbRepository>();
            PadronCargados = await repo.CountAsync();
            var ult = await repo.UltimaFechaPublicacionAsync();
            PadronUltimaPublicacion = ult is null ? null : ult.Value.ToDateTime(TimeOnly.MinValue);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "ActualizarStatsPadron"); }
    }
}
