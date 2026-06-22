using FacturacionArca.Application.UseCases;
using FacturacionArca.Domain.Padrones;

namespace FacturacionArca.Infrastructure.Padrones;

public sealed class PadronAgipParserService : IPadronAgipParserService
{
    public IEnumerable<PadronIibbCaba> Parse(string ruta, CancellationToken ct) =>
        PadronAgipParser.Parse(ruta, ct);

    public PadronIibbCaba? ParseLine(string linea) =>
        PadronAgipParser.ParseLine(linea);
}
