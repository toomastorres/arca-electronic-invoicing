using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Padrones;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

public sealed class ImportarPadronAgip
{
    private readonly IPadronIibbRepository _repo;
    private readonly IPadronAgipParserService _parser;
    private readonly ILogger<ImportarPadronAgip> _logger;

    public ImportarPadronAgip(
        IPadronIibbRepository repo,
        IPadronAgipParserService parser,
        ILogger<ImportarPadronAgip> logger)
    {
        _repo = repo;
        _parser = parser;
        _logger = logger;
    }

    public async Task<ResultadoImportacion> EjecutarAsync(string rutaArchivo, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(rutaArchivo))
            return new ResultadoImportacion(0, default, "Archivo no encontrado");

        var primeraLinea = ObtenerPrimeraLinea(rutaArchivo);
        var primeraEntry = primeraLinea is null ? null : _parser.ParseLine(primeraLinea);
        if (primeraEntry is null)
            return new ResultadoImportacion(0, default, "Formato de archivo no reconocido");

        var fechaPub = primeraEntry.FechaPublicacion;
        _logger.LogInformation("Importando padrón AGIP fechaPub={FechaPub}", fechaPub);

        var stream = ToAsyncEnumerable(_parser.Parse(rutaArchivo, ct), progress, ct);
        var total = await _repo.ReemplazarLoteAsync(fechaPub, stream, ct);

        _logger.LogInformation("Padrón AGIP importado: {Total} entradas (publicación {FechaPub})", total, fechaPub);
        return new ResultadoImportacion(total, fechaPub, null);
    }

    private static string? ObtenerPrimeraLinea(string ruta)
    {
        using var sr = new StreamReader(ruta);
        return sr.ReadLine();
    }

    private static async IAsyncEnumerable<PadronIibbCaba> ToAsyncEnumerable(
        IEnumerable<PadronIibbCaba> source,
        IProgress<int>? progress,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var i = 0;
        foreach (var item in source)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            if (++i % 10_000 == 0)
            {
                progress?.Report(i);
                await Task.Yield();
            }
        }
        progress?.Report(i);
    }
}

public record ResultadoImportacion(int CantidadCargada, DateOnly FechaPublicacion, string? Error);

/// <summary>
/// Abstracción del parser para que la capa Application no dependa de Infrastructure.
/// </summary>
public interface IPadronAgipParserService
{
    IEnumerable<PadronIibbCaba> Parse(string ruta, CancellationToken ct);
    PadronIibbCaba? ParseLine(string linea);
}
