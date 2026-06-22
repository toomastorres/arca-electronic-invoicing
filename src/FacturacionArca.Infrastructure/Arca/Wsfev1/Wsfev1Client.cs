using System.Text;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Infrastructure.Configuracion;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Infrastructure.Arca.Wsfev1;

public sealed class Wsfev1Client : IArcaWsfev1Client
{
    private const string Namespace = "http://ar.gov.afip.dif.FEV1/";

    private readonly HttpClient _http;
    private readonly IArcaWsaaClient _wsaa;
    private readonly IConfiguracionRepository _configRepo;
    private readonly ILogger<Wsfev1Client> _logger;

    public Wsfev1Client(HttpClient http, IArcaWsaaClient wsaa, IConfiguracionRepository configRepo, ILogger<Wsfev1Client> logger)
    {
        _http = http;
        _wsaa = wsaa;
        _configRepo = configRepo;
        _logger = logger;
    }

    public async Task<long> ObtenerUltimoAutorizadoAsync(int puntoVenta, TipoComprobante tipo, CancellationToken ct = default)
    {
        var (ta, cuit, url) = await ContextoAsync(ct);
        var soap = Wsfev1RequestBuilder.FECompUltimoAutorizado(ta, cuit, puntoVenta, (int)tipo);
        var resp = await EnviarAsync(url, "FECompUltimoAutorizado", soap, ct);
        return Wsfev1ResponseParser.ParseUltimoAutorizado(resp);
    }

    public async Task<RespuestaCae> SolicitarCaeAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        var (ta, cuit, url) = await ContextoAsync(ct);
        var soap = Wsfev1RequestBuilder.FECAESolicitar(ta, cuit, comprobante);
        var resp = await EnviarAsync(url, "FECAESolicitar", soap, ct);
        return Wsfev1ResponseParser.ParseFECAESolicitar(resp);
    }

    public async Task<Comprobante?> ConsultarComprobanteAsync(int puntoVenta, TipoComprobante tipo, long numero, CancellationToken ct = default)
    {
        var (ta, cuit, url) = await ContextoAsync(ct);
        var soap = Wsfev1RequestBuilder.FECompConsultar(ta, cuit, puntoVenta, (int)tipo, numero);
        var resp = await EnviarAsync(url, "FECompConsultar", soap, ct);
        return Wsfev1ResponseParser.ParseFECompConsultar(resp, puntoVenta, tipo, numero);
    }

    public async Task<IReadOnlyList<ParametroAfip>> ObtenerTiposComprobanteAsync(CancellationToken ct = default) =>
        await ParametroGenericoAsync("FEParamGetTiposCbte", "ResultGet", "Id", "Desc", ct);

    public async Task<IReadOnlyList<ParametroAfip>> ObtenerTiposDocumentoAsync(CancellationToken ct = default) =>
        await ParametroGenericoAsync("FEParamGetTiposDoc", "ResultGet", "Id", "Desc", ct);

    public async Task<IReadOnlyList<ParametroAfip>> ObtenerTiposIvaAsync(CancellationToken ct = default) =>
        await ParametroGenericoAsync("FEParamGetTiposIva", "ResultGet", "Id", "Desc", ct);

    public async Task<IReadOnlyList<ParametroAfip>> ObtenerTiposMonedaAsync(CancellationToken ct = default) =>
        await ParametroGenericoAsync("FEParamGetTiposMonedas", "ResultGet", "Id", "Desc", ct);

    public async Task<decimal> ObtenerCotizacionAsync(string monedaId, DateOnly fecha, CancellationToken ct = default)
    {
        var (ta, cuit, url) = await ContextoAsync(ct);
        var soap = Wsfev1RequestBuilder.FEParamGetCotizacion(ta, cuit, monedaId);
        var resp = await EnviarAsync(url, "FEParamGetCotizacion", soap, ct);
        return Wsfev1ResponseParser.ParseCotizacion(resp);
    }

    private async Task<IReadOnlyList<ParametroAfip>> ParametroGenericoAsync(
        string metodo, string nodoColeccion, string codTag, string descTag, CancellationToken ct)
    {
        var (ta, cuit, url) = await ContextoAsync(ct);
        var soap = Wsfev1RequestBuilder.FEParamGet(ta, cuit, metodo);
        var resp = await EnviarAsync(url, metodo, soap, ct);
        return Wsfev1ResponseParser.ParseParametros(resp, nodoColeccion, codTag, descTag);
    }

    private async Task<(Domain.Wsaa.TicketAcceso ta, string cuit, string url)> ContextoAsync(CancellationToken ct)
    {
        var cfg = await _configRepo.GetAsync(ct);
        var ta = await _wsaa.ObtenerTicketAsync("wsfe", ct);
        var url = ArcaEndpoints.Para(cfg.Modo).Wsfev1Url;
        return (ta, cfg.Cuit, url);
    }

    private async Task<string> EnviarAsync(string url, string metodo, string soap, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(soap, Encoding.UTF8, "text/xml"),
        };
        req.Headers.Add("SOAPAction", $"\"{Namespace}{metodo}\"");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        sw.Stop();

        _logger.LogInformation("WSFEv1.{Metodo} → HTTP {Status} en {Ms} ms", metodo, (int)resp.StatusCode, sw.ElapsedMilliseconds);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("WSFEv1.{Metodo}: HTTP error. Body: {Body}", metodo, body);
            throw new HttpRequestException($"WSFEv1.{metodo} respondió HTTP {(int)resp.StatusCode}");
        }

        return body;
    }
}
