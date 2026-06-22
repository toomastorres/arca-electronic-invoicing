using FacturacionArca.Domain.Proformas;

namespace FacturacionArca.Application.Abstractions;

public interface IProformaXmlParser
{
    ProformaNapoles Parse(string xmlContent, string archivoOrigen);
}
