using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Infrastructure.Arca.Wsfecred;

/// <summary>
/// Cliente para Factura de Crédito Electrónica MiPyMEs.
/// La emisión del comprobante FCE (CbteTipo 201/202/203/206/207/208/211/212/213) se realiza
/// efectivamente contra <c>WSFEv1.FECAESolicitar</c> con los Opcionales que ARCA define
/// (Opcional Id 2101 para CBU, 27 para "Optar SCA Adherido", etc.). El servicio WSFECRED
/// propiamente dicho expone consultas y eventos de aceptación/rechazo del lado del comprador,
/// y se modela como API separada para evolución posterior.
/// </summary>
public sealed class WsfecredClient : IArcaWsfecredClient
{
    private readonly IArcaWsfev1Client _wsfev1;
    private readonly ILogger<WsfecredClient> _logger;

    public WsfecredClient(IArcaWsfev1Client wsfev1, ILogger<WsfecredClient> logger)
    {
        _wsfev1 = wsfev1;
        _logger = logger;
    }

    public Task<RespuestaCae> EmitirFceAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        if (!comprobante.Tipo.EsFce())
            throw new InvalidOperationException(
                $"El comprobante tipo {comprobante.Tipo} no es FCE MiPyMEs.");

        _logger.LogInformation("Emitiendo FCE MiPyMEs {Tipo} vía WSFEv1.FECAESolicitar.", comprobante.Tipo);
        return _wsfev1.SolicitarCaeAsync(comprobante, ct);
    }

    public Task<EstadoFce> ConsultarEstadoFceAsync(int puntoVenta, TipoComprobante tipo, long numero, CancellationToken ct = default)
    {
        // Pendiente: implementar WSFECRED.ConsultarFCECred contra el endpoint
        // ArcaEndpoints.Para(modo).WsfecredUrl en una iteración posterior.
        _logger.LogWarning("Consulta de estado FCE aún no implementada (puntoVenta={Pto}, tipo={Tipo}, nro={Nro}).",
            puntoVenta, tipo, numero);
        return Task.FromResult(new EstadoFce("PENDIENTE", "Consulta WSFECRED pendiente de integración.", null));
    }
}
