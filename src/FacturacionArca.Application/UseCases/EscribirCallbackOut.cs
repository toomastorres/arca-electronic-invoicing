using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FacturacionArca.Domain.Comprobantes;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

/// <summary>
/// Escribe el XML de callback que el ERP Nápoles espera en la carpeta OUT
/// para confirmar que la facturación fue exitosa.
/// Formato fijo: ga{numProforma}.xml
///   &lt;Comprobante&gt;
///     &lt;Proforma&gt;2020T0000090&lt;/Proforma&gt;
///     &lt;CodigoTipoComprobante&gt;01 &lt;/CodigoTipoComprobante&gt;
///     &lt;NumeroPuntoventa&gt;0004&lt;/NumeroPuntoventa&gt;
///     &lt;NumeroComprobante&gt;00008380&lt;/NumeroComprobante&gt;
///     &lt;Caenumero&gt;70053815341741&lt;/Caenumero&gt;
///     &lt;Caefecha&gt;20200130&lt;/Caefecha&gt;
///     &lt;Peribcaba&gt;267.15&lt;/Peribcaba&gt;
///   &lt;/Comprobante&gt;
/// </summary>
public sealed class EscribirCallbackOut
{
    private readonly ILogger<EscribirCallbackOut> _logger;
    public EscribirCallbackOut(ILogger<EscribirCallbackOut> logger) => _logger = logger;

    public async Task<string?> EjecutarAsync(Comprobante c, string carpetaOut, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(carpetaOut)) return null;
        if (c.Cae is null)
        {
            _logger.LogWarning("No se escribe callback OUT: comprobante sin CAE.");
            return null;
        }

        Directory.CreateDirectory(carpetaOut);

        var perIbCaba = c.Tributos
            .Where(t => t.IdAfip == Tributo.IdIngresosBrutos)
            .Sum(t => t.Importe);

        var doc = new XDocument(
            new XElement("Comprobante",
                new XElement("Proforma", c.NumeroProforma),
                // WARN-04: Para tipos FCE (201+) usar 3+ dígitos; para normales (1-13) usar 2 dígitos.
                // El espacio final se mantiene por compatibilidad con el formato original del ERP.
                new XElement("CodigoTipoComprobante", $"{(int)c.Tipo:D2} "),
                new XElement("NumeroPuntoventa", $"{c.PuntoVenta:D4}"),
                new XElement("NumeroComprobante", $"{c.Numero:D8}"),
                new XElement("Caenumero", c.Cae.Numero),
                new XElement("Caefecha", c.Cae.FechaProceso.ToString("yyyyMMdd")),
                new XElement("Peribcaba", perIbCaba.ToString("F2", CultureInfo.InvariantCulture))
            )
        );

        var nombreBase = !string.IsNullOrWhiteSpace(c.NumeroProforma) ? c.NumeroProforma : $"AFIP_{c.PuntoVenta:D4}_{c.Numero:D8}";
        // Convención observada: ga{nro_proforma_lower}.xml
        var nombre = $"ga{nombreBase.ToLowerInvariant()}.xml";
        var path = Path.Combine(carpetaOut, nombre);

        // Escribir SIN declaración XML, con encoding ASCII-compat (replicar lo más posible al original)
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        await using var sw = new StreamWriter(fs, new UTF8Encoding(false));
        await sw.WriteAsync(doc.ToString(SaveOptions.DisableFormatting));
        await sw.FlushAsync(ct);

        _logger.LogInformation("Callback OUT escrito: {Path}", path);
        return path;
    }
}
