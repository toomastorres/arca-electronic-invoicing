using Xunit;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Wsaa;
using FacturacionArca.Infrastructure.Arca.Wsfev1;
using FluentAssertions;

namespace FacturacionArca.Tests;

public class FceYSaldosTests
{
    private static Comprobante BuildBaseFce(bool? esSCA = true, string? cbu = null, string? alias = null)
    {
        var c = new Comprobante
        {
            Tipo = TipoComprobante.FacturaCreditoMipymeA,
            Concepto = Concepto.Servicios,
            PuntoVenta = 1,
            Numero = 100,
            FechaEmision = new DateOnly(2026, 4, 28),
            FechaServicioDesde = new DateOnly(2026, 4, 1),
            FechaServicioHasta = new DateOnly(2026, 4, 28),
            FechaVencimientoPago = new DateOnly(2026, 5, 15),
            CodigoMoneda = "PES",
            CotizacionMoneda = 1m,
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m,
            EsSCA = esSCA,
            CbuFce = cbu,
            AliasFce = alias,
            Receptor = new ReceptorComprobante
            {
                CodigoTipoDocumento = 80,
                NumeroDocumento = "30000000007",
                CondicionIva = CondicionIva.ResponsableInscripto,
            },
        };
        c.SubtotalesIva.Add(new SubtotalIva { CodigoAlicuotaAfip = 5, BaseImponible = 100m, Importe = 21m });
        return c;
    }

    [Fact]
    public void FCE_SCA_GeneraOpcional27ValorS()
    {
        var ta = new TicketAcceso { Token = "T", Sign = "S" };
        var c = BuildBaseFce(esSCA: true);

        var soap = Wsfev1RequestBuilder.FECAESolicitar(ta, "30000000099", c);

        soap.Should().Contain("<ar:Opcionales>");
        soap.Should().Contain("<ar:Id>27</ar:Id>");
        soap.Should().Contain("<ar:Valor>S</ar:Valor>");
    }

    [Fact]
    public void FCE_ADC_GeneraOpcional27ValorN()
    {
        var ta = new TicketAcceso { Token = "T", Sign = "S" };
        var c = BuildBaseFce(esSCA: false);

        var soap = Wsfev1RequestBuilder.FECAESolicitar(ta, "30000000099", c);

        soap.Should().Contain("<ar:Id>27</ar:Id>");
        soap.Should().Contain("<ar:Valor>N</ar:Valor>");
    }

    [Fact]
    public void FCE_ConCbuYAlias_AgregaOpcionales2101Y2102()
    {
        var ta = new TicketAcceso { Token = "T", Sign = "S" };
        var c = BuildBaseFce(esSCA: true, cbu: "0110000000000000000001", alias: "AMA.ARG.MP");

        var soap = Wsfev1RequestBuilder.FECAESolicitar(ta, "30000000099", c);

        soap.Should().Contain("<ar:Id>2101</ar:Id>");
        soap.Should().Contain("<ar:Valor>0110000000000000000001</ar:Valor>");
        soap.Should().Contain("<ar:Id>2102</ar:Id>");
        soap.Should().Contain("<ar:Valor>AMA.ARG.MP</ar:Valor>");
    }

    [Fact]
    public void FacturaA_NoFce_NoIncluyeOpcionales()
    {
        var ta = new TicketAcceso { Token = "T", Sign = "S" };
        var c = BuildBaseFce();
        c.Tipo = TipoComprobante.FacturaA;
        c.EsSCA = null;

        var soap = Wsfev1RequestBuilder.FECAESolicitar(ta, "30000000099", c);

        soap.Should().NotContain("<ar:Opcionales>");
    }

    [Fact]
    public void TipoSaldoCtaCte_PorDefecto_EsPesificada()
    {
        var c = new Comprobante();
        c.TipoSaldo.Should().Be(TipoSaldoCtaCte.Pesificado);
        c.TipoSaldo.Codigo().Should().Be("P");
    }

    [Fact]
    public void TipoSaldo_S_GeneraCodigoYDescripcionCorrectos()
    {
        TipoSaldoCtaCte.EnMonedaOriginal.Codigo().Should().Be("S");
        TipoSaldoCtaCte.EnMonedaOriginal.Descripcion().Should().Contain("moneda original");
    }
}
