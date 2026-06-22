using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Application.Abstractions;

public interface IArcaWsfecredClient
{
    Task<RespuestaCae> EmitirFceAsync(Comprobante comprobante, CancellationToken ct = default);
    Task<EstadoFce> ConsultarEstadoFceAsync(int puntoVenta, TipoComprobante tipo, long numero, CancellationToken ct = default);
}

public sealed record EstadoFce(string CodigoEstado, string Descripcion, DateTime? FechaAceptacionRechazo);
