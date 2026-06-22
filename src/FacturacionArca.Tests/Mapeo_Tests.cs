using Xunit;
using FacturacionArca.Application.UseCases;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Infrastructure.XmlNapoles;
using FluentAssertions;

namespace FacturacionArca.Tests;

public class Mapeo_Tests
{
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void Mapeo_FacturaA_USD_DeberiaPreservarMontosYCotizacion()
    {
        var path = Path.Combine(FixturesDir, "AFIP_AFIPOB_8554454506.XML");
        var contenido = File.ReadAllText(path);
        var parser = new ProformaXmlParser();
        var proforma = parser.Parse(contenido, Path.GetFileName(path));

        var cfg = new ConfiguracionEmpresa { Cuit = "30000000007", PuntoVentaPorDefecto = 1 };
        var mapeador = new MapearProformaAComprobante();
        var opciones = new OpcionesMapeo
        {
            Concepto = Concepto.Servicios,
            FechaServicioDesde = new DateOnly(2016, 3, 1),
            FechaServicioHasta = new DateOnly(2016, 3, 4),
            FechaVencimientoPago = new DateOnly(2016, 3, 18),
            CondicionIngresosBrutos = "Convenio Multilateral",
        };

        var c = mapeador.Ejecutar(proforma, cfg, opciones);

        c.Tipo.Should().Be(TipoComprobante.FacturaA);
        c.PuntoVenta.Should().Be(1);
        c.CodigoMoneda.Should().Be("DOL");
        c.CotizacionMoneda.Should().Be(10m);
        c.ImporteNeto.Should().Be(500m);
        c.ImporteNoGravado.Should().Be(800m);
        c.ImporteIva.Should().Be(105m);
        c.ImporteTotal.Should().Be(1405m);
        c.Receptor.CondicionIva.Should().Be(CondicionIva.ResponsableInscripto);
        c.Receptor.CodigoTipoDocumento.Should().Be(80);
        c.Items.Should().NotBeEmpty();
        c.SubtotalesIva.Should().ContainSingle(s => s.CodigoAlicuotaAfip == 5);
    }

    [Fact]
    public void Mapeo_NotaCreditoA_DeberiaNormalizarSigno()
    {
        var path = Path.Combine(FixturesDir, "AFIP_AFIPOB_8554778431.XML");
        var contenido = File.ReadAllText(path);
        var parser = new ProformaXmlParser();
        var proforma = parser.Parse(contenido, Path.GetFileName(path));

        var cfg = new ConfiguracionEmpresa { Cuit = "30000000007", PuntoVentaPorDefecto = 1 };
        var mapeador = new MapearProformaAComprobante();

        var c = mapeador.Ejecutar(proforma, cfg, new OpcionesMapeo());

        c.Tipo.Should().Be(TipoComprobante.NotaCreditoA);
        c.ImporteTotal.Should().Be(60.50m);
        c.ImporteIva.Should().Be(10.50m);
    }
}
