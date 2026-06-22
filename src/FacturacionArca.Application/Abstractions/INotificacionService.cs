namespace FacturacionArca.Application.Abstractions;

/// <summary>
/// Nivel de severidad de una notificación para el usuario.
/// </summary>
public enum NivelNotificacion
{
    /// <summary>Información general (color azul).</summary>
    Info,
    /// <summary>Operación exitosa (color verde).</summary>
    Exito,
    /// <summary>Advertencia — acción puede ser necesaria (color naranja).</summary>
    Advertencia,
    /// <summary>Error que requiere atención inmediata (color rojo).</summary>
    Error,
}

/// <summary>
/// Modelo de una notificación para mostrar al usuario.
/// </summary>
public sealed record Notificacion(
    NivelNotificacion Nivel,
    string Titulo,
    string Mensaje,
    string? CodigoError = null,
    string? AccionSugerida = null,
    DateTime? Timestamp = null)
{
    public DateTime TimestampReal => Timestamp ?? DateTime.Now;
}

/// <summary>
/// Servicio centralizado de notificaciones al usuario.
/// Todas las capas (Application, Infrastructure, UI) pueden publicar notificaciones,
/// y la capa UI las muestra como toasts/alertas en pantalla.
/// </summary>
public interface INotificacionService
{
    /// <summary>Publica una notificación para el usuario.</summary>
    void Notificar(Notificacion notificacion);

    /// <summary>Atajos rápidos.</summary>
    void Info(string titulo, string mensaje);
    void Exito(string titulo, string mensaje);
    void Advertencia(string titulo, string mensaje, string? codigoError = null, string? accionSugerida = null);
    void Error(string titulo, string mensaje, string? codigoError = null, string? accionSugerida = null);

    /// <summary>Evento que la UI suscribe para mostrar toasts.</summary>
    event Action<Notificacion>? OnNotificacion;
}
