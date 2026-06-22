using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using Microsoft.Extensions.DependencyInjection;

namespace FacturacionArca.Wpf.ViewModels;

public partial class HistorialComprobantesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Comprobante> Resultados { get; } = new();

    [ObservableProperty] private string? texto;
    [ObservableProperty] private DateTime? desde = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime? hasta = DateTime.Today;

    public HistorialComprobantesViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = BuscarAsync();
    }

    [RelayCommand]
    public async Task BuscarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IComprobanteRepository>();
        var d = Desde is null ? (DateOnly?)null : DateOnly.FromDateTime(Desde.Value);
        var h = Hasta is null ? (DateOnly?)null : DateOnly.FromDateTime(Hasta.Value);
        var lista = await repo.SearchAsync(Texto, d, h);
        Resultados.Clear();
        foreach (var c in lista) Resultados.Add(c);
    }
}
