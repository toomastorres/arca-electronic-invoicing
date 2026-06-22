using FacturacionArca.Domain.Configuracion;

namespace FacturacionArca.Infrastructure.Configuracion;

public sealed class ArcaEndpoints
{
    public string WsaaUrl { get; init; } = "";
    public string Wsfev1Url { get; init; } = "";
    public string WsfecredUrl { get; init; } = "";

    public static ArcaEndpoints Para(ModoOperacion modo) => modo switch
    {
        ModoOperacion.Homologacion => new()
        {
            WsaaUrl = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
            Wsfev1Url = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
            WsfecredUrl = "https://fwshomo.afip.gov.ar/wsfecred/FECredService",
        },
        ModoOperacion.Produccion => new()
        {
            WsaaUrl = "https://wsaa.afip.gov.ar/ws/services/LoginCms",
            Wsfev1Url = "https://servicios1.afip.gov.ar/wsfev1/service.asmx",
            WsfecredUrl = "https://serviciosjava.afip.gob.ar/wsfecred/FECredService",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(modo)),
    };
}
