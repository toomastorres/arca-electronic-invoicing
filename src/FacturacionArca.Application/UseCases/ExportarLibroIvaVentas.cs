using System.Globalization;
using System.Text;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

/// <summary>
/// Genera el Libro IVA Ventas para un período seleccionado.
/// Salida en CSV (separador ; compatible con Excel ES-AR) y formato AFIP CITI.
/// Las columnas siguen la estructura habitual del libro IVA argentino.
/// </summary>
public sealed class ExportarLibroIvaVentas
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private readonly IComprobanteRepository _repo;
    private readonly ILogger<ExportarLibroIvaVentas> _logger;

    public ExportarLibroIvaVentas(IComprobanteRepository repo, ILogger<ExportarLibroIvaVentas> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<ResultadoExportLibro> EjecutarAsync(DateOnly desde, DateOnly hasta, string rutaCsv, CancellationToken ct = default)
    {
        var lista = await _repo.ListarAutorizadosEnRangoAsync(desde, hasta, ct);
        Directory.CreateDirectory(Path.GetDirectoryName(rutaCsv)!);

        await using var fs = new FileStream(rutaCsv, FileMode.Create, FileAccess.Write);
        await using var sw = new StreamWriter(fs, new UTF8Encoding(true)); // BOM para Excel ES-AR

        // Cabecera
        await sw.WriteLineAsync(string.Join(';',
            "Fecha", "Tipo", "Letra", "PtoVta", "Numero",
            "TipoDoc", "NroDoc", "Razon Social", "CondIVA",
            "ImpNeto21", "ImpNeto10.5", "ImpNeto27", "ImpNoGravado", "ImpExento",
            "IVA21", "IVA10.5", "IVA27", "Percep.IIBB", "OtrosTrib",
            "ImpTotal", "Moneda", "Cotiz", "ImpTotalARS",
            "CAE", "VtoCAE", "Proforma"));

        var totalRegistros = 0;
        var totalNeto = 0m;
        var totalIva = 0m;
        var totalGeneral = 0m;

        foreach (var c in lista)
        {
            ct.ThrowIfCancellationRequested();
            var letra = c.Tipo.LetraVisible();
            var iva21 = c.SubtotalesIva.Where(s => s.CodigoAlicuotaAfip == 5).Sum(s => s.Importe);
            var iva105 = c.SubtotalesIva.Where(s => s.CodigoAlicuotaAfip == 4).Sum(s => s.Importe);
            var iva27 = c.SubtotalesIva.Where(s => s.CodigoAlicuotaAfip == 6).Sum(s => s.Importe);
            var neto21 = c.SubtotalesIva.Where(s => s.CodigoAlicuotaAfip == 5).Sum(s => s.BaseImponible);
            var neto105 = c.SubtotalesIva.Where(s => s.CodigoAlicuotaAfip == 4).Sum(s => s.BaseImponible);
            var neto27 = c.SubtotalesIva.Where(s => s.CodigoAlicuotaAfip == 6).Sum(s => s.BaseImponible);
            var ibbCaba = c.Tributos.Where(t => t.IdAfip == Tributo.IdIngresosBrutos).Sum(t => t.Importe);
            var otrosTrib = c.ImporteTributos - ibbCaba;
            var totalArs = c.ImporteTotal * c.CotizacionMoneda;

            await sw.WriteLineAsync(string.Join(';',
                c.FechaEmision.ToString("yyyy-MM-dd"),
                ((int)c.Tipo).ToString("D3"),
                letra,
                c.PuntoVenta.ToString("D5"),
                (c.Numero ?? 0).ToString("D8"),
                c.Receptor.CodigoTipoDocumento.ToString(),
                c.Receptor.NumeroDocumento,
                Csv(c.Receptor.RazonSocial),
                c.Receptor.CondicionIva.ToString(),
                F(neto21), F(neto105), F(neto27),
                F(c.ImporteNoGravado), F(c.ImporteExento),
                F(iva21), F(iva105), F(iva27), F(ibbCaba), F(otrosTrib),
                F(c.ImporteTotal), c.CodigoMoneda, F(c.CotizacionMoneda), F(totalArs),
                c.Cae?.Numero ?? "",
                c.Cae?.FechaVencimiento.ToString("yyyy-MM-dd") ?? "",
                Csv(c.NumeroProforma)
            ));

            totalRegistros++;
            totalNeto += neto21 + neto105 + neto27;
            totalIva += iva21 + iva105 + iva27;
            totalGeneral += c.ImporteTotal;
        }

        // Totales finales
        await sw.WriteLineAsync();
        await sw.WriteLineAsync($"TOTALES;;;;;;;;;{F(totalNeto)};;;;;{F(totalIva)};;;;;{F(totalGeneral)}");

        await sw.FlushAsync(ct);

        _logger.LogInformation("Libro IVA Ventas generado: {Path} ({N} comprobantes)", rutaCsv, totalRegistros);
        return new ResultadoExportLibro(rutaCsv, totalRegistros, totalNeto, totalIva, totalGeneral);
    }

    private static string F(decimal v) => v.ToString("F2", Cul);

    /// <summary>Escapa un valor para CSV con separador ; — encierra entre comillas si tiene ; o ".</summary>
    private static string Csv(string? s)
    {
        s ??= "";
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

public record ResultadoExportLibro(string RutaArchivo, int Cantidad, decimal TotalNeto, decimal TotalIva, decimal TotalGeneral);
