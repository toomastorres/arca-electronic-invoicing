using System.Diagnostics;
using System.Drawing.Printing;
using FacturacionArca.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Infrastructure.Printing;

public sealed class SilentPdfPrinter : IPrinterService
{
    private readonly ILogger<SilentPdfPrinter> _logger;

    public SilentPdfPrinter(ILogger<SilentPdfPrinter> logger) => _logger = logger;

    public IReadOnlyList<string> ListarImpresoras()
    {
        var lista = new List<string>();
        foreach (var n in PrinterSettings.InstalledPrinters)
            if (n is string s) lista.Add(s);
        return lista;
    }

    public Task ImprimirPdfAsync(string pdfPath, string? nombreImpresora = null, bool silencioso = false, CancellationToken ct = default)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException("PDF no encontrado.", pdfPath);

        var verb = silencioso ? "printto" : "print";
        var arguments = silencioso && !string.IsNullOrWhiteSpace(nombreImpresora) ? $"\"{nombreImpresora}\"" : "";

        var psi = new ProcessStartInfo
        {
            FileName = pdfPath,
            Verb = verb,
            Arguments = arguments,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var p = Process.Start(psi);
            _logger.LogInformation("Impresión enviada: {Path} → {Impresora} (silencioso={Silencioso})",
                pdfPath, nombreImpresora ?? "default", silencioso);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al imprimir {Path}", pdfPath);
            throw;
        }

        return Task.CompletedTask;
    }
}
