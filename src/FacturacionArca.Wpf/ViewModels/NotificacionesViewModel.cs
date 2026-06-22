using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FacturacionArca.Application.Abstractions;

namespace FacturacionArca.Wpf.ViewModels;

/// <summary>
/// Modelo para una notificación visible en la UI (toast).
/// Se auto-elimina después de N segundos según severidad.
/// </summary>
public partial class NotificacionToastItem : ObservableObject
{
    public Notificacion Notificacion { get; }
    public string Icono => Notificacion.Nivel switch
    {
        NivelNotificacion.Exito => "✅",
        NivelNotificacion.Advertencia => "⚠️",
        NivelNotificacion.Error => "❌",
        _ => "ℹ️",
    };
    public string ColorFondo => Notificacion.Nivel switch
    {
        NivelNotificacion.Exito => "#E8F5E9",
        NivelNotificacion.Advertencia => "#FFF3E0",
        NivelNotificacion.Error => "#FFEBEE",
        _ => "#E3F2FD",
    };
    public string ColorBorde => Notificacion.Nivel switch
    {
        NivelNotificacion.Exito => "#4CAF50",
        NivelNotificacion.Advertencia => "#FF9800",
        NivelNotificacion.Error => "#F44336",
        _ => "#2196F3",
    };

    [ObservableProperty] private bool visible = true;

    public NotificacionToastItem(Notificacion notificacion) => Notificacion = notificacion;
}

/// <summary>
/// ViewModel que gestiona la cola de notificaciones toast visibles en MainWindow.
/// </summary>
public sealed class NotificacionesViewModel
{
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<NotificacionToastItem> Toasts { get; } = new();

    /// <summary>Historial completo de notificaciones (para panel de admin).</summary>
    public ObservableCollection<Notificacion> Historial { get; } = new();

    public NotificacionesViewModel(INotificacionService servicio)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        servicio.OnNotificacion += OnNuevaNotificacion;
    }

    private void OnNuevaNotificacion(Notificacion n)
    {
        _dispatcher.Invoke(() =>
        {
            Historial.Insert(0, n);
            if (Historial.Count > 200) Historial.RemoveAt(200);

            var item = new NotificacionToastItem(n);
            Toasts.Add(item);

            // Auto-dismiss: errores 12s, advertencias 8s, info/éxito 5s
            var duracion = n.Nivel switch
            {
                NivelNotificacion.Error => 12000,
                NivelNotificacion.Advertencia => 8000,
                _ => 5000,
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(duracion) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Toasts.Remove(item);
            };
            timer.Start();
        });
    }
}
