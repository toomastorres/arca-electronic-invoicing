using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Proformas;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

public sealed class ImportarProformaXml
{
    private readonly IProformaXmlParser _parser;
    private readonly IProformaRepository _repo;
    private readonly ILogger<ImportarProformaXml> _logger;

    public ImportarProformaXml(IProformaXmlParser parser, IProformaRepository repo, ILogger<ImportarProformaXml> logger)
    {
        _parser = parser;
        _repo = repo;
        _logger = logger;
    }

    public async Task<ProformaNapoles> EjecutarAsync(string xmlContent, string archivoOrigen, CancellationToken ct = default)
    {
        var existente = await _repo.FindByArchivoAsync(archivoOrigen, ct);
        if (existente is not null)
        {
            _logger.LogInformation("Proforma {Archivo} ya importada (id={Id}, estado={Estado}).",
                archivoOrigen, existente.Id, existente.Estado);
            return existente;
        }

        var proforma = _parser.Parse(xmlContent, archivoOrigen);
        proforma.Estado = EstadoProforma.Pendiente;
        proforma.FechaImportacion = DateTime.UtcNow;
        proforma.XmlCrudo = xmlContent;
        await _repo.AddAsync(proforma, ct);

        _logger.LogInformation("Proforma {Numero} importada desde {Archivo} (cliente={Cliente}, total={Total} {Mon}).",
            proforma.NumeroProforma, archivoOrigen, proforma.Cliente, proforma.ImporteTotal, proforma.CodigoMoneda);
        return proforma;
    }
}
