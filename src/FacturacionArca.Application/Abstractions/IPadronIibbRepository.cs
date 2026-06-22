using FacturacionArca.Domain.Padrones;

namespace FacturacionArca.Application.Abstractions;

public interface IPadronIibbRepository
{
    /// <summary>Cargar lote de entradas eliminando primero los padrones de la misma fecha publicación.</summary>
    Task<int> ReemplazarLoteAsync(DateOnly fechaPublicacion, IAsyncEnumerable<PadronIibbCaba> entradas, CancellationToken ct = default);

    /// <summary>Buscar la entrada vigente para un CUIT a una fecha dada (default: hoy).</summary>
    Task<PadronIibbCaba?> BuscarVigenteAsync(string cuit, DateOnly? a = null, CancellationToken ct = default);

    /// <summary>Cantidad total de entradas activas (para diagnostico).</summary>
    Task<int> CountAsync(CancellationToken ct = default);

    /// <summary>Última fecha de publicación cargada.</summary>
    Task<DateOnly?> UltimaFechaPublicacionAsync(CancellationToken ct = default);
}
