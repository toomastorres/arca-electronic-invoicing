using System.Globalization;
using System.Xml.Linq;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Errores;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Infrastructure.Arca.Wsfev1;

public static class Wsfev1ResponseParser
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static long ParseUltimoAutorizado(string soapXml)
    {
        var doc = XDocument.Parse(soapXml);
        VerificarErrores(doc, soapXml);
        var nro = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "CbteNro")?.Value;
        return long.TryParse(nro, NumberStyles.Integer, Inv, out var n) ? n : 0L;
    }

    public static RespuestaCae ParseFECAESolicitar(string soapXml)
    {
        var doc = XDocument.Parse(soapXml);
        VerificarErrores(doc, soapXml);

        var det = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "FECAEDetResponse")
            ?? throw new InvalidOperationException("Respuesta sin <FECAEDetResponse>.");

        var resultado = det.Elements().FirstOrDefault(e => e.Name.LocalName == "Resultado")?.Value ?? "?";
        var cae = det.Elements().FirstOrDefault(e => e.Name.LocalName == "CAE")?.Value ?? "";
        var cbteDesde = det.Elements().FirstOrDefault(e => e.Name.LocalName == "CbteDesde")?.Value;
        var caeFchVto = det.Elements().FirstOrDefault(e => e.Name.LocalName == "CAEFchVto")?.Value;

        var cabecera = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "FeCabResp");
        var fchProceso = cabecera?.Elements().FirstOrDefault(e => e.Name.LocalName == "FchProceso")?.Value;

        if (resultado != "A")
        {
            var observaciones = det.Descendants()
                .Where(e => e.Name.LocalName == "Obs")
                .Select(o => new ArcaError(
                    int.TryParse(o.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value, out var c) ? c : 0,
                    o.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value ?? ""))
                .ToList();

            if (observaciones.Count == 0)
                observaciones.Add(new ArcaError(0, $"AFIP rechazó el comprobante (Resultado={resultado})."));

            throw new ArcaException(observaciones, requestXml: null, responseXml: soapXml);
        }

        var nro = long.TryParse(cbteDesde, NumberStyles.Integer, Inv, out var n) ? n : 0L;
        var vto = ParseDateOnly(caeFchVto);
        var proc = ParseDateTime(fchProceso);

        return new RespuestaCae(cae, vto, proc, nro, resultado, soapXml);
    }

    public static Comprobante? ParseFECompConsultar(string soapXml, int ptoVta, TipoComprobante tipo, long numero)
    {
        var doc = XDocument.Parse(soapXml);
        try { VerificarErrores(doc, soapXml); }
        catch (ArcaException ex) when (ex.Errores.Any(e => e.Codigo == 602)) { return null; }

        var resultGet = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResultGet");
        if (resultGet is null || !resultGet.HasElements) return null;

        var caeNumero = resultGet.Elements().FirstOrDefault(e => e.Name.LocalName == "CodAutorizacion")?.Value
                     ?? resultGet.Elements().FirstOrDefault(e => e.Name.LocalName == "CAE")?.Value;
        var caeVto = resultGet.Elements().FirstOrDefault(e => e.Name.LocalName == "FchVto")?.Value;
        var fchProc = resultGet.Elements().FirstOrDefault(e => e.Name.LocalName == "FchProceso")?.Value;

        if (string.IsNullOrWhiteSpace(caeNumero)) return null;

        var c = new Comprobante
        {
            PuntoVenta = ptoVta,
            Tipo = tipo,
            Numero = numero,
            Cae = new Cae(caeNumero!, ParseDateOnly(caeVto), ParseDateTime(fchProc)),
            RespuestaArcaXml = soapXml,
        };
        return c;
    }

    public static IReadOnlyList<ParametroAfip> ParseParametros(string soapXml, string nodoColeccion, string codigoTag, string descTag)
    {
        var doc = XDocument.Parse(soapXml);
        VerificarErrores(doc, soapXml);

        var coleccion = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == nodoColeccion);
        if (coleccion is null) return Array.Empty<ParametroAfip>();

        return coleccion.Elements().Select(it =>
        {
            var codigo = it.Elements().FirstOrDefault(x => x.Name.LocalName == codigoTag)?.Value ?? "";
            var desc = it.Elements().FirstOrDefault(x => x.Name.LocalName == descTag)?.Value ?? "";
            var vDesde = it.Elements().FirstOrDefault(x => x.Name.LocalName == "FchDesde")?.Value;
            var vHasta = it.Elements().FirstOrDefault(x => x.Name.LocalName == "FchHasta")?.Value;
            return new ParametroAfip(codigo, desc, ParseDateOnlyOrNull(vDesde), ParseDateOnlyOrNull(vHasta));
        }).ToList();
    }

    public static decimal ParseCotizacion(string soapXml)
    {
        var doc = XDocument.Parse(soapXml);
        VerificarErrores(doc, soapXml);
        var v = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "MonCotiz")?.Value;
        return decimal.TryParse(v, NumberStyles.Number, Inv, out var d) ? d : 0m;
    }

    private static void VerificarErrores(XDocument doc, string soapXml)
    {
        var faults = doc.Descendants().Where(e => e.Name.LocalName == "Fault").ToList();
        if (faults.Count > 0)
        {
            var msg = string.Join(" | ", faults.Select(f =>
                f.Elements().FirstOrDefault(x => x.Name.LocalName == "faultstring")?.Value ?? f.Value));
            throw new ArcaException(new[] { new ArcaError(500, msg) }, null, soapXml);
        }

        var errores = doc.Descendants()
            .Where(e => e.Name.LocalName == "Errors" && e.HasElements)
            .SelectMany(e => e.Elements().Where(x => x.Name.LocalName == "Err"))
            .Select(err =>
            {
                int code = 0;
                int.TryParse(err.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value, out code);
                var msg = err.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value ?? "";
                return new ArcaError(code, msg);
            })
            .ToList();

        if (errores.Count > 0) throw new ArcaException(errores, null, soapXml);
    }

    // WARN-05: Loguear warning cuando ARCA devuelve fecha vacía o no parseable
    private static DateOnly ParseDateOnly(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
        {
            Serilog.Log.Warning("ARCA devolvió fecha vacía; se asume fecha de hoy ({Hoy}). Verificar respuesta XML.", DateTime.Today.ToString("yyyy-MM-dd"));
            return DateOnly.FromDateTime(DateTime.Today);
        }
        if (DateOnly.TryParseExact(v, "yyyyMMdd", Inv, DateTimeStyles.None, out var d)) return d;
        if (DateOnly.TryParse(v, Inv, DateTimeStyles.None, out var d2)) return d2;
        Serilog.Log.Warning("No se pudo parsear fecha ARCA '{Valor}'; se asume hoy.", v);
        return DateOnly.FromDateTime(DateTime.Today);
    }

    private static DateOnly? ParseDateOnlyOrNull(string? v) =>
        string.IsNullOrWhiteSpace(v) || v == "NULL"
            ? null
            : (DateOnly.TryParseExact(v, "yyyyMMdd", Inv, DateTimeStyles.None, out var d) ? d : null);

    private static DateTime ParseDateTime(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
        {
            Serilog.Log.Warning("ARCA devolvió timestamp vacío; se asume ahora UTC.");
            return DateTime.UtcNow;
        }
        if (DateTime.TryParseExact(v, "yyyyMMddHHmmss", Inv, DateTimeStyles.AssumeLocal, out var dt))
            return dt.ToUniversalTime();
        if (DateTime.TryParse(v, Inv, DateTimeStyles.AssumeLocal, out var dt2))
            return dt2.ToUniversalTime();
        Serilog.Log.Warning("No se pudo parsear timestamp ARCA '{Valor}'; se asume ahora UTC.", v);
        return DateTime.UtcNow;
    }
}
