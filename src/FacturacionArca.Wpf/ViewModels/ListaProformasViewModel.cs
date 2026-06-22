using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Application.UseCases;
using FacturacionArca.Domain.Proformas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Wpf.ViewModels;

public partial class ListaProformasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProformaWatcher _watcher;
    private readonly ILogger<ListaProformasViewModel> _logger;

    public ObservableCollection<ProformaNapoles> Proformas { get; } = new();

    [ObservableProperty] private ProformaNapoles? selectedProforma;
    [ObservableProperty] private bool soloPendientes = true;
    [ObservableProperty] private string? mensajeEstado;

    public ListaProformasViewModel(
        IServiceScopeFactory scopeFactory,
        IProformaWatcher watcher,
        ILogger<ListaProformasViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _watcher = watcher;
        _logger = logger;

        _watcher.ProformaDetectada += OnProformaDetectada;
        _ = RefrescarAsync();
    }

    [RelayCommand]
    public async Task RefrescarAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IProformaRepository>();
            var lista = await repo.ListAsync(SoloPendientes ? EstadoProforma.Pendiente : null);
            Proformas.Clear();
            foreach (var p in lista) Proformas.Add(p);
            MensajeEstado = $"{Proformas.Count} proformas listadas.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al refrescar lista de proformas.");
            MensajeEstado = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ImportarManualAsync(string archivo)
    {
        if (string.IsNullOrWhiteSpace(archivo) || !File.Exists(archivo))
        {
            MensajeEstado = "Archivo inválido.";
            return;
        }

        try
        {
            var contenido = await File.ReadAllTextAsync(archivo);
            using var scope = _scopeFactory.CreateScope();
            var importar = scope.ServiceProvider.GetRequiredService<ImportarProformaXml>();
            var p = await importar.EjecutarAsync(contenido, archivo);
            await RefrescarAsync();
            SelectedProforma = Proformas.FirstOrDefault(x => x.Id == p.Id);
            MensajeEstado = $"Proforma {p.NumeroProforma} importada.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al importar manualmente {Archivo}.", archivo);
            MensajeEstado = $"Error al importar: {ex.Message}";
        }
    }

    private async void OnProformaDetectada(object? sender, ProformaDetectadaEventArgs e)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var importar = scope.ServiceProvider.GetRequiredService<ImportarProformaXml>();
            await importar.EjecutarAsync(e.Contenido, e.ArchivoCompleto);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(RefrescarAsync);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al importar proforma detectada {Archivo}.", e.ArchivoCompleto);
        }
    }

    partial void OnSoloPendientesChanged(bool value) => _ = RefrescarAsync();
}
