using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Application.UseCases;

public sealed class ImprimirComprobante
{
    private readonly IPrinterService _printer;
    private readonly IConfiguracionRepository _config;

    public ImprimirComprobante(IPrinterService printer, IConfiguracionRepository config)
    {
        _printer = printer;
        _config = config;
    }

    public async Task EjecutarAsync(Comprobante c, string? impresora = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(c.PdfPath) || !File.Exists(c.PdfPath))
            throw new InvalidOperationException("El comprobante no tiene PDF generado. Genere primero el PDF.");

        var cfg = await _config.GetAsync(ct);
        var nombre = impresora ?? cfg.ImpresoraPorDefecto;
        await _printer.ImprimirPdfAsync(c.PdfPath, nombre, cfg.ImpresionSilenciosa, ct);
    }
}
