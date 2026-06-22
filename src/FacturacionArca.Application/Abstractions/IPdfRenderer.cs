using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Application.Abstractions;

public interface IPdfRenderer
{
    Task<byte[]> RenderizarAsync(Comprobante comprobante, CancellationToken ct = default);
    Task<string> RenderizarYGuardarAsync(Comprobante comprobante, string carpetaSalida, CancellationToken ct = default);
}

public interface IQrAfipBuilder
{
    string ConstruirUrl(Comprobante comprobante, string cuitEmisor);
    byte[] GenerarPng(string url);
}

public interface IPrinterService
{
    Task ImprimirPdfAsync(string pdfPath, string? nombreImpresora = null, bool silencioso = false, CancellationToken ct = default);
    IReadOnlyList<string> ListarImpresoras();
}

public interface IArchivoFiscalRepository
{
    Task<string> GuardarRespuestaAsync(Comprobante comprobante, string xmlRespuesta, string carpetaBase, CancellationToken ct = default);
}
