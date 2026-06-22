using Xunit;
using System.Xml.Linq;
using FacturacionArca.Infrastructure.Arca.Wsaa;
using FluentAssertions;

namespace FacturacionArca.Tests;

public class TraBuilder_Tests
{
    [Fact]
    public void Construir_DeberiaProducirXmlValidoConCamposObligatorios()
    {
        var xml = TraBuilder.Construir("wsfe");

        var doc = XDocument.Parse(xml);
        var root = doc.Root!;
        root.Name.LocalName.Should().Be("loginTicketRequest");

        var header = root.Element("header")!;
        header.Element("uniqueId")!.Value.Should().NotBeNullOrEmpty();
        header.Element("generationTime")!.Value.Should().Match(s => s.Length >= 19);
        header.Element("expirationTime")!.Value.Should().Match(s => s.Length >= 19);

        root.Element("service")!.Value.Should().Be("wsfe");
    }
}
