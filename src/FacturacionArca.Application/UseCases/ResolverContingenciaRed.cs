using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

public sealed class ResolverContingenciaRed
{
    private readonly IArcaWsfev1Client _wsfe;
    private readonly ILogger<ResolverContingenciaRed> _logger;

    public ResolverContingenciaRed(IArcaWsfev1Client wsfe, ILogger<ResolverContingenciaRed> logger)
    {
        _wsfe = wsfe;
        _logger = logger;
    }

    public async Task<RespuestaCae?> EjecutarAsync(Comprobante c, CancellationToken ct = default)
    {
        if (c.Numero is null) return null;

        _logger.LogInformation("Contingencia: consultando comprobante {Tipo} {Pto}-{Nro} en AFIP para verificar si fue procesado.",
            c.Tipo, c.PuntoVenta, c.Numero);

        var existente = await _wsfe.ConsultarComprobanteAsync(c.PuntoVenta, c.Tipo, c.Numero.Value, ct);
        if (existente is null || existente.Cae is null)
        {
            _logger.LogInformation("Contingencia: AFIP no tiene registro del comprobante. Procede el reintento de emisión.");
            return null;
        }

        _logger.LogWarning("Contingencia: AFIP ya tenía el CAE {Cae}; se asume aprobado por el primer intento.",
            existente.Cae.Numero);

        return new RespuestaCae(
            existente.Cae.Numero,
            existente.Cae.FechaVencimiento,
            existente.Cae.FechaProceso,
            c.Numero.Value,
            "A",
            existente.RespuestaArcaXml ?? "");
    }
}
