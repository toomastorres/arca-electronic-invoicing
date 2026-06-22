using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Domain.Errores;
using FacturacionArca.Domain.Wsaa;
using FacturacionArca.Infrastructure.Configuracion;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Infrastructure.Arca.Wsaa;

public sealed class WsaaClient : IArcaWsaaClient
{
    private static readonly TimeSpan MargenRefrescoTicket = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;
    private readonly ITicketAccesoCache _cache;
    private readonly IConfiguracionRepository _configRepo;
    private readonly ILogger<WsaaClient> _logger;
    private readonly ICertificadoProvider _certificadoProvider;

    public WsaaClient(
        HttpClient http,
        ITicketAccesoCache cache,
        IConfiguracionRepository configRepo,
        ICertificadoProvider certificadoProvider,
        ILogger<WsaaClient> logger)
    {
        _http = http;
        _cache = cache;
        _configRepo = configRepo;
        _certificadoProvider = certificadoProvider;
        _logger = logger;
    }

    public async Task<TicketAcceso> ObtenerTicketAsync(string servicio, CancellationToken ct = default)
    {
        var cfg = await _configRepo.GetAsync(ct);
        var modo = cfg.Modo.ToString();
        var existente = await _cache.GetAsync(servicio, cfg.Cuit, modo, ct);

        if (existente is not null && existente.EsVigente(DateTime.UtcNow, MargenRefrescoTicket))
        {
            _logger.LogDebug("WSAA: usando TA en cache para {Servicio} (vto {Vto:O}).", servicio, existente.ExpirationTime);
            return existente;
        }

        return await RenovarTicketAsync(servicio, ct);
    }

    public async Task<TicketAcceso> RenovarTicketAsync(string servicio, CancellationToken ct = default)
    {
        var cfg = await _configRepo.GetAsync(ct);
        var endpoints = ArcaEndpoints.Para(cfg.Modo);

        using var cert = _certificadoProvider.Cargar(cfg.CertificadoPath, cfg.CertificadoPassword);
        var tra = TraBuilder.Construir(servicio);
        var cmsBase64 = ArcaCmsSigner.FirmarBase64(tra, cert);

        var soapEnvelope = ConstruirSoap(cmsBase64);

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoints.WsaaUrl)
        {
            Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml"),
        };
        req.Headers.Add("SOAPAction", "");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("WSAA: HTTP {Status}. Body: {Body}", (int)resp.StatusCode, body);
            throw new ArcaException(new[] { new ArcaError((int)resp.StatusCode, body) }, soapEnvelope, body);
        }

        var ticket = ExtraerTicket(body, servicio, cfg);
        await _cache.SaveAsync(ticket, ct);
        _logger.LogInformation("WSAA: TA emitido para {Servicio} (gen {Gen:O}, exp {Exp:O}).",
            servicio, ticket.GenerationTime, ticket.ExpirationTime);
        return ticket;
    }

    private static string ConstruirSoap(string cmsBase64) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:wsaa="http://wsaa.view.sua.dvadac.desein.afip.gov">
          <soapenv:Header/>
          <soapenv:Body>
            <wsaa:loginCms>
              <wsaa:in0>{cmsBase64}</wsaa:in0>
            </wsaa:loginCms>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private static TicketAcceso ExtraerTicket(string soapResponse, string servicio, ConfiguracionEmpresa cfg)
    {
        var doc = XDocument.Parse(soapResponse);
        var loginReturn = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")
            ?? throw new InvalidOperationException("WSAA: respuesta sin <loginCmsReturn>.");

        var inner = XDocument.Parse(loginReturn.Value);
        var token = inner.Descendants("token").FirstOrDefault()?.Value
            ?? throw new InvalidOperationException("WSAA: respuesta sin <token>.");
        var sign = inner.Descendants("sign").FirstOrDefault()?.Value
            ?? throw new InvalidOperationException("WSAA: respuesta sin <sign>.");
        var generation = inner.Descendants("generationTime").FirstOrDefault()?.Value;
        var expiration = inner.Descendants("expirationTime").FirstOrDefault()?.Value;

        return new TicketAcceso
        {
            Servicio = servicio,
            Token = token,
            Sign = sign,
            GenerationTime = DateTime.Parse(generation ?? DateTime.UtcNow.ToString("O"), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(),
            ExpirationTime = DateTime.Parse(expiration ?? DateTime.UtcNow.AddHours(12).ToString("O"), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(),
            Cuit = cfg.Cuit,
            Modo = cfg.Modo.ToString(),
        };
    }
}

public interface ICertificadoProvider
{
    X509Certificate2 Cargar(string path, string? password);
}

public sealed class FileSystemCertificadoProvider : ICertificadoProvider
{
    public X509Certificate2 Cargar(string path, string? password)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("No hay un certificado configurado. Configure la ruta del .pfx en Configuración.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Certificado no encontrado en {path}.", path);

        var flags = X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable;
        return new X509Certificate2(path, password ?? "", flags);
    }
}
