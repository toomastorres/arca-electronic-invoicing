using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Application.UseCases;

public sealed class GenerarPdfComprobante
{
    private readonly IPdfRenderer _renderer;
    private readonly IConfiguracionRepository _config;
    private readonly IComprobanteRepository _repo;

    public GenerarPdfComprobante(IPdfRenderer renderer, IConfiguracionRepository config, IComprobanteRepository repo)
    {
        _renderer = renderer;
        _config = config;
        _repo = repo;
    }

    public async Task<string> EjecutarAsync(Comprobante c, CancellationToken ct = default)
    {
        var cfg = await _config.GetAsync(ct);
        var carpeta = string.IsNullOrWhiteSpace(cfg.CarpetaPdfSalida)
            ? Path.Combine(AppContext.BaseDirectory, "pdf")
            : cfg.CarpetaPdfSalida;

        Directory.CreateDirectory(carpeta);
        var path = await _renderer.RenderizarYGuardarAsync(c, carpeta, ct);
        c.PdfPath = path;
        await _repo.UpdateAsync(c, ct);
        return path;
    }
}
