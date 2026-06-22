using FacturacionArca.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Infrastructure;

/// <summary>
/// Implementación del servicio de notificaciones.
/// Publica notificaciones y las loguea automáticamente.
/// </summary>
public sealed class NotificacionService : INotificacionService
{
    private readonly ILogger<NotificacionService> _logger;

    public NotificacionService(ILogger<NotificacionService> logger) => _logger = logger;

    public event Action<Notificacion>? OnNotificacion;

    public void Notificar(Notificacion notificacion)
    {
        // Log automático según nivel
        var logMsg = $"[{notificacion.Nivel}] {notificacion.Titulo}: {notificacion.Mensaje}";
        if (!string.IsNullOrWhiteSpace(notificacion.CodigoError))
            logMsg += $" (Código: {notificacion.CodigoError})";
        if (!string.IsNullOrWhiteSpace(notificacion.AccionSugerida))
            logMsg += $" → Acción: {notificacion.AccionSugerida}";

        switch (notificacion.Nivel)
        {
            case NivelNotificacion.Error:
                _logger.LogError(logMsg);
                break;
            case NivelNotificacion.Advertencia:
                _logger.LogWarning(logMsg);
                break;
            case NivelNotificacion.Exito:
                _logger.LogInformation(logMsg);
                break;
            default:
                _logger.LogInformation(logMsg);
                break;
        }

        OnNotificacion?.Invoke(notificacion);
    }

    public void Info(string titulo, string mensaje) =>
        Notificar(new Notificacion(NivelNotificacion.Info, titulo, mensaje));

    public void Exito(string titulo, string mensaje) =>
        Notificar(new Notificacion(NivelNotificacion.Exito, titulo, mensaje));

    public void Advertencia(string titulo, string mensaje, string? codigoError = null, string? accionSugerida = null) =>
        Notificar(new Notificacion(NivelNotificacion.Advertencia, titulo, mensaje, codigoError, accionSugerida));

    public void Error(string titulo, string mensaje, string? codigoError = null, string? accionSugerida = null) =>
        Notificar(new Notificacion(NivelNotificacion.Error, titulo, mensaje, codigoError, accionSugerida));
}
