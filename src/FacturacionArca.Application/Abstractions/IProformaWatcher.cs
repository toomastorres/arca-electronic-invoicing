namespace FacturacionArca.Application.Abstractions;

public interface IProformaWatcher : IDisposable
{
    event EventHandler<ProformaDetectadaEventArgs>? ProformaDetectada;
    void Iniciar(string carpeta);
    void Detener();
    Task EscanearExistentesAsync(CancellationToken ct = default);
}

public sealed class ProformaDetectadaEventArgs : EventArgs
{
    public required string ArchivoCompleto { get; init; }
    public required string Contenido { get; init; }
    public DateTime FechaDeteccion { get; init; } = DateTime.UtcNow;
}
