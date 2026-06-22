using System.Globalization;
using System.Text;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

/// <summary>
/// Genera dos archivos TXT (anchos fijos AGIP) para entregar al estudio contable:
///   - Percepciones de Facturas / Notas de Débito (Régimen General).
///   - Percepciones de Notas de Crédito (devolución de percepciones).
/// Solo incluye comprobantes con percepción IIBB CABA &gt; 0.
/// </summary>
public sealed class ExportarAgipPercepcionesTxt
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private readonly IComprobanteRepository _repo;
    private readonly ILogger<ExportarAgipPercepcionesTxt> _logger;

    public ExportarAgipPercepcionesTxt(IComprobanteRepository repo, ILogger<ExportarAgipPercepcionesTxt> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<ResultadoAgipExport> EjecutarAsync(
        DateOnly desde, DateOnly hasta,
        string rutaFacturas, string rutaNotasCredito,
        CancellationToken ct = default)
    {
        var lista = await _repo.ListarAutorizadosEnRangoAsync(desde, hasta, ct);

        var facturas = lista.Where(c => !c.Tipo.EsNotaCredito() && PercepcionIbb(c) > 0).ToList();
        var ncs = lista.Where(c => c.Tipo.EsNotaCredito() && PercepcionIbb(c) > 0).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(rutaFacturas)!);
        Directory.CreateDirectory(Path.GetDirectoryName(rutaNotasCredito)!);

        await using (var fs = new FileStream(rutaFacturas, FileMode.Create, FileAccess.Write))
        await using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
        {
            foreach (var c in facturas)
                await sw.WriteLineAsync(LineaFactura(c));
        }

        await using (var fs = new FileStream(rutaNotasCredito, FileMode.Create, FileAccess.Write))
        await using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
        {
            foreach (var c in ncs)
                await sw.WriteLineAsync(LineaNotaCredito(c));
        }

        _logger.LogInformation("AGIP exportado: {NF} facturas, {NN} NC. Período {D:yyyy-MM-dd}..{H:yyyy-MM-dd}",
            facturas.Count, ncs.Count, desde, hasta);

        return new ResultadoAgipExport(rutaFacturas, rutaNotasCredito, facturas.Count, ncs.Count);
    }

    private static decimal PercepcionIbb(Comprobante c) =>
        c.Tributos.Where(t => t.IdAfip == Tributo.IdIngresosBrutos).Sum(t => t.Importe);

    /// <summary>
    /// Línea para registro de PERCEPCIÓN (factura / ND).
    /// Formato AGIP régimen general — campos de longitud fija sin separador.
    /// </summary>
    private static string LineaFactura(Comprobante c)
    {
        var perc = PercepcionIbb(c);
        var alic = c.Tributos.FirstOrDefault(t => t.IdAfip == Tributo.IdIngresosBrutos)?.Alicuota ?? 0m;
        var letraNum = (int)c.Tipo;
        var letraStr = c.Tipo.LetraVisible();
        var ptoVta = c.PuntoVenta;
        var nro = c.Numero ?? 0;
        var totalArs = c.ImporteTotal * c.CotizacionMoneda;
        var gravado = c.ImporteNeto * c.CotizacionMoneda;
        var noGravado = c.ImporteNoGravado * c.CotizacionMoneda;
        var fecha = c.FechaEmision.ToString("dd/MM/yyyy");
        var razon = (c.Receptor.RazonSocial ?? "").ToUpperInvariant();
        if (razon.Length > 30) razon = razon[..30];
        razon = razon.PadRight(30);
        var cuit = (c.Receptor.NumeroDocumento ?? "").PadLeft(11, '0');

        // 4-char prefijo (período mes/año) + fecha + tipo + letra + ptoVta + nro + fecha original + montototal +
        // 16 espacios + tipoDoc(1) + cuit(11) + flag(1) + cuit(11) + flag(1) + razon(30) +
        // gravado + noGravado + percepBase + signo+alic + percepImporte + percepImporte (duplicado)
        var sb = new StringBuilder(220);
        sb.Append("2029");                                               // 4 chars
        sb.Append(fecha);                                                // 10 chars
        sb.Append(letraNum.ToString("D2"));                              // 2 chars
        sb.Append(letraStr);                                             // 1 char
        sb.Append(ptoVta.ToString("D8"));                                // 8 chars
        sb.Append(nro.ToString("D8"));                                   // 8 chars
        sb.Append(fecha);                                                // 10 chars (mismo)
        sb.Append(NumeroFijo(totalArs, 16));                             // monto total
        sb.Append(' ', 16);                                              // 16 espacios
        sb.Append('3');                                                  // 1 char tipoDoc
        sb.Append(cuit);                                                 // 11 chars cuit
        sb.Append('2');                                                  // 1 char flag
        sb.Append(cuit);                                                 // 11 chars cuit
        sb.Append('1');                                                  // 1 char flag
        sb.Append(razon);                                                // 30 chars
        sb.Append(NumeroFijo(gravado, 16));                              // gravado
        sb.Append(NumeroFijo(noGravado, 16));                            // no gravado
        sb.Append(NumeroFijo(c.Tributos.FirstOrDefault(t => t.IdAfip == Tributo.IdIngresosBrutos)?.BaseImponible ?? 0m, 16));
        sb.Append(AlicuotaFija(alic));                                   // 5 chars (signo+ % "00,50")
        sb.Append(NumeroFijo(perc, 17));                                 // percep importe
        sb.Append(NumeroFijo(perc, 17));                                 // percep importe duplicado
        sb.Append("           ");                                        // padding final
        return sb.ToString();
    }

    /// <summary>Línea para registro de DEVOLUCIÓN de percepción (NC).</summary>
    private static string LineaNotaCredito(Comprobante c)
    {
        var perc = PercepcionIbb(c);
        var alic = c.Tributos.FirstOrDefault(t => t.IdAfip == Tributo.IdIngresosBrutos)?.Alicuota ?? 0m;
        var letraNum = (int)c.Tipo;
        var letraStr = c.Tipo.LetraVisible();
        var ptoVta = c.PuntoVenta;
        var nro = c.Numero ?? 0;
        var totalArs = Math.Abs(c.ImporteTotal * c.CotizacionMoneda);
        var fechaCbte = c.FechaEmision.ToString("dd/MM/yyyy");
        var asoc = c.ComprobantesAsociados.FirstOrDefault();
        var fechaCbteOrig = asoc?.FechaEmision?.ToString("dd/MM/yyyy") ?? c.FechaEmision.ToString("dd/MM/yyyy");
        var ptoVtaOrig = asoc?.PuntoVenta ?? c.PuntoVenta;
        var nroOrig = asoc?.Numero ?? (c.Numero ?? 0);
        var cuit = (c.Receptor.NumeroDocumento ?? "").PadLeft(11, '0');

        var sb = new StringBuilder(180);
        sb.Append('2');                                                   // tipo registro = 2 (NC devolución)
        sb.Append(' ');                                                   // separador
        sb.Append(nro.ToString("D11"));                                   // ID interno
        sb.Append(fechaCbte);                                             // fecha NC
        sb.Append(NumeroFijo(Math.Abs(perc) > 0m ? totalArs : 0m, 16));   // monto NC
        sb.Append(' ', 18);                                               // padding
        sb.Append(letraNum.ToString("D2"));                               // tipo cbte original
        sb.Append(letraStr);                                              // letra
        sb.Append(ptoVtaOrig.ToString("D8"));                             // ptoVta original
        sb.Append(nroOrig.ToString("D8"));                                // nro original
        sb.Append(cuit);                                                  // cuit
        sb.Append("0292");                                                // régimen NC
        sb.Append(fechaCbteOrig);                                         // fecha cbte original
        sb.Append(NumeroFijo(Math.Abs(perc), 17));                        // importe percepción a devolver
        sb.Append(AlicuotaFija(alic));                                    // alícuota
        return sb.ToString();
    }

    /// <summary>Numero a string ancho fijo con coma decimal y 2 decimales (ej "0000001253116,00").</summary>
    private static string NumeroFijo(decimal valor, int largo)
    {
        var s = Math.Abs(valor).ToString("F2", Cul).Replace('.', ',');
        return s.PadLeft(largo, '0');
    }

    /// <summary>Alícuota porcentual con coma (ej 0.50 → "00,50").</summary>
    private static string AlicuotaFija(decimal alic)
    {
        var s = alic.ToString("F2", Cul).Replace('.', ',');
        return s.PadLeft(5, '0');
    }
}

public record ResultadoAgipExport(string RutaFacturas, string RutaNotasCredito, int CantidadFacturas, int CantidadNotasCredito);
