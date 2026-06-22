using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Application.UseCases;

/// <summary>
/// Calcula la percepción IIBB CABA aplicable a un receptor según el padrón AGIP vigente.
/// Si el receptor no figura en el padrón, no se aplica percepción (alícuota 0).
/// </summary>
public sealed class CalcularPercepcionIibbCaba
{
    private readonly IPadronIibbRepository _repo;
    public CalcularPercepcionIibbCaba(IPadronIibbRepository repo) => _repo = repo;

    public async Task<PercepcionCalculada> EjecutarAsync(string cuit, decimal baseImponible, DateOnly? fecha = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cuit) || baseImponible <= 0)
            return PercepcionCalculada.NoAplica;

        var entry = await _repo.BuscarVigenteAsync(cuit, fecha, ct);
        if (entry is null || entry.AlicuotaPercepcion <= 0m)
            return PercepcionCalculada.NoAplica;

        var importe = Math.Round(baseImponible * entry.AlicuotaPercepcion / 100m, 2, MidpointRounding.AwayFromZero);
        return new PercepcionCalculada(true, "CABA", entry.AlicuotaPercepcion, baseImponible, importe, entry.RegimenPercepcion);
    }
}

public record PercepcionCalculada(
    bool Aplica,
    string Jurisdiccion,
    decimal Alicuota,
    decimal BaseImponible,
    decimal Importe,
    string? RegimenAgip)
{
    public static PercepcionCalculada NoAplica { get; } = new(false, "CABA", 0m, 0m, 0m, null);
}
