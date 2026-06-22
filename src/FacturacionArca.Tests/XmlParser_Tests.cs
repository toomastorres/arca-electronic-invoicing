using Xunit;
using FacturacionArca.Infrastructure.XmlNapoles;
using FluentAssertions;

namespace FacturacionArca.Tests;

public class XmlParser_Tests
{
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static IEnumerable<object[]> TodosLosXml() =>
        Directory.EnumerateFiles(FixturesDir, "*.XML").Select(p => new object[] { Path.GetFileName(p) });

    [Theory]
    [MemberData(nameof(TodosLosXml))]
    public void Parser_DeberiaProcesarTodosLosXmlSinExcepciones(string archivo)
    {
        var path = Path.Combine(FixturesDir, archivo);
        var contenido = File.ReadAllText(path);
        var parser = new ProformaXmlParser();

        var p = parser.Parse(contenido, archivo);

        p.NumeroProforma.Should().NotBeNullOrWhiteSpace();
        p.CodigoTipoComprobanteOrigen.Should().BeGreaterThan(0);
        p.NumeroDocumento.Should().NotBeNullOrWhiteSpace();
        p.Cliente.Should().NotBeNullOrWhiteSpace();
        p.ImporteTotal.Should().NotBe(0m, "los XML reales tienen total != 0");
        p.CodigoMoneda.Should().NotBeNullOrWhiteSpace();
        p.Items.Should().NotBeEmpty();
    }

    [Fact]
    public void Parser_DeberiaParsearProforma2016T0000057_FacturaA()
    {
        var path = Path.Combine(FixturesDir, "AFIP_AFIPOB_8554454506.XML");
        var contenido = File.ReadAllText(path);
        var parser = new ProformaXmlParser();

        var p = parser.Parse(contenido, "AFIP_AFIPOB_8554454506.XML");

        p.NumeroProforma.Should().Be("2016T0000057");
        p.CodigoTipoComprobanteOrigen.Should().Be(1);
        p.FechaEmision.Should().Be(new DateOnly(2016, 3, 4));
        p.CodigoTipoDocumento.Should().Be(80);
        p.NumeroDocumento.Should().Be("30711111117");
        p.Cliente.Should().Contain("DEMO");
        p.IvaCondicionTexto.Should().Be("RESPONSABLE INSCRIPTO");
        p.CodigoMoneda.Should().Be("DOL");
        p.CotizacionMoneda.Should().Be(10m);
        p.ImporteGravado.Should().Be(500m);
        p.ImporteNoGravado.Should().Be(800m);
        p.ImporteTotal.Should().Be(1405m);
        p.SubtotalesIva.Should().HaveCount(1);
        p.SubtotalesIva[0].Codigo.Should().Be(5);
        p.SubtotalesIva[0].Importe.Should().Be(105m);
        p.Items.Should().HaveCountGreaterThan(5);
        p.BuqueViaje.Should().Be("ATLANTIC STAR 0116");
        p.PorCuenta.Should().Contain("Atlantic");
    }

    [Fact]
    public void Parser_DeberiaParsearNotaCreditoA_ConImportesNegativos()
    {
        var path = Path.Combine(FixturesDir, "AFIP_AFIPOB_8554778431.XML");
        var contenido = File.ReadAllText(path);
        var parser = new ProformaXmlParser();

        var p = parser.Parse(contenido, "AFIP_AFIPOB_8554778431.XML");

        p.CodigoTipoComprobanteOrigen.Should().Be(3);
        p.ImporteGravado.Should().Be(-50.00m);
        p.ImporteIvaRi.Should().Be(-10.50m);
        p.ImporteTotal.Should().Be(-60.50m);
        p.CodigoMoneda.Should().Be("PES");
    }
}
