using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturacionArca.Application.Abstractions;

namespace FacturacionArca.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    [ObservableProperty]
    private object? currentView;

    /// <summary>ViewModel de notificaciones toast (compartido con la UI).</summary>
    public NotificacionesViewModel Notificaciones { get; }

    public MainViewModel(IServiceProvider sp, INotificacionService notificacionService)
    {
        _sp = sp;
        Notificaciones = new NotificacionesViewModel(notificacionService);
        ShowProformas();
    }

    [RelayCommand] private void ShowProformas() => CurrentView = Resolve<ListaProformasViewModel>();
    [RelayCommand] private void ShowHistorial() => CurrentView = Resolve<HistorialComprobantesViewModel>();
    [RelayCommand] private void ShowExportaciones() => CurrentView = Resolve<ExportacionesViewModel>();
    [RelayCommand] private void ShowConfiguracion() => CurrentView = Resolve<ConfiguracionViewModel>();

    private T Resolve<T>() where T : notnull =>
        (T)_sp.GetService(typeof(T))!;
}
