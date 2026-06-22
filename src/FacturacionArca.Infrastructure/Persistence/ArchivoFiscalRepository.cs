using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Infrastructure.Persistence;

public sealed class ArchivoFiscalRepository : IArchivoFiscalRepository
{
    public async Task<string> GuardarRespuestaAsync(Comprobante c, string xmlRespuesta, string carpetaBase, CancellationToken ct = default)
    {
        var carpeta = Path.Combine(carpetaBase, c.FechaEmision.Year.ToString(), c.FechaEmision.Month.ToString("00"));
        Directory.CreateDirectory(carpeta);

        var nombre = $"CAE_{(int)c.Tipo:D3}_{c.PuntoVenta:D5}_{c.Numero:D8}_{c.FechaEmision:yyyyMMdd}.xml";
        var path = Path.Combine(carpeta, nombre);

        await File.WriteAllTextAsync(path, xmlRespuesta, ct);
        return path;
    }
}
