using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Application.Abstractions;

public interface IArcaWsfev1Client
{
    Task<long> ObtenerUltimoAutorizadoAsync(int puntoVenta, TipoComprobante tipo, CancellationToken ct = default);
    Task<RespuestaCae> SolicitarCaeAsync(Comprobante comprobante, CancellationToken ct = default);
    Task<Comprobante?> ConsultarComprobanteAsync(int puntoVenta, TipoComprobante tipo, long numero, CancellationToken ct = default);
    Task<IReadOnlyList<ParametroAfip>> ObtenerTiposComprobanteAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ParametroAfip>> ObtenerTiposDocumentoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ParametroAfip>> ObtenerTiposIvaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ParametroAfip>> ObtenerTiposMonedaAsync(CancellationToken ct = default);
    Task<decimal> ObtenerCotizacionAsync(string monedaId, DateOnly fecha, CancellationToken ct = default);
}

public sealed record ParametroAfip(string Codigo, string Descripcion, DateOnly? VigenciaDesde = null, DateOnly? VigenciaHasta = null);

public sealed record RespuestaCae(
    string Cae,
    DateOnly FechaVencimiento,
    DateTime FechaProceso,
    long NumeroComprobante,
    string Resultado,
    string XmlRespuesta);
