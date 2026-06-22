using FacturacionArca.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

public sealed class SincronizarMaestrosArca
{
    private readonly IArcaWsfev1Client _wsfe;
    private readonly ILogger<SincronizarMaestrosArca> _logger;

    public SincronizarMaestrosArca(IArcaWsfev1Client wsfe, ILogger<SincronizarMaestrosArca> logger)
    {
        _wsfe = wsfe;
        _logger = logger;
    }

    public async Task<MaestrosArca> EjecutarAsync(CancellationToken ct = default)
    {
        var tareas = new[]
        {
            _wsfe.ObtenerTiposComprobanteAsync(ct),
            _wsfe.ObtenerTiposDocumentoAsync(ct),
            _wsfe.ObtenerTiposIvaAsync(ct),
            _wsfe.ObtenerTiposMonedaAsync(ct),
        };

        await Task.WhenAll(tareas);
        var (tiposCbte, tiposDoc, tiposIva, tiposMoneda) = (tareas[0].Result, tareas[1].Result, tareas[2].Result, tareas[3].Result);

        _logger.LogInformation("Maestros sincronizados: {Cbte} cbte, {Doc} doc, {Iva} IVA, {Mon} monedas.",
            tiposCbte.Count, tiposDoc.Count, tiposIva.Count, tiposMoneda.Count);

        return new MaestrosArca(tiposCbte, tiposDoc, tiposIva, tiposMoneda);
    }
}

public sealed record MaestrosArca(
    IReadOnlyList<ParametroAfip> TiposComprobante,
    IReadOnlyList<ParametroAfip> TiposDocumento,
    IReadOnlyList<ParametroAfip> TiposIva,
    IReadOnlyList<ParametroAfip> TiposMoneda);
