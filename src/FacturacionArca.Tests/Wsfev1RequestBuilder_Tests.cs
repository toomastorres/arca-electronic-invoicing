using Xunit;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Wsaa;
using FacturacionArca.Infrastructure.Arca.Wsfev1;
using FluentAssertions;

namespace FacturacionArca.Tests;

public class Wsfev1RequestBuilder_Tests
{
    [Fact]
    public void FECAESolicitar_DeberiaIncluirCamposObligatorios()
    {
        var ta = new TicketAcceso { Token = "TKN", Sign = "SGN" };
        var c = new Comprobante
        {
            Tipo = TipoComprobante.FacturaA,
            Concepto = Concepto.Servicios,
            PuntoVenta = 1,
            Numero = 100,
            FechaEmision = new DateOnly(2026, 4, 28),
            FechaServicioDesde = new DateOnly(2026, 4, 1),
            FechaServicioHasta = new DateOnly(2026, 4, 28),
            FechaVencimientoPago = new DateOnly(2026, 5, 15),
            CodigoMoneda = "DOL",
            CotizacionMoneda = 1180.5m,
            ImporteNeto = 100m,
            ImporteNoGravado = 0m,
            ImporteExento = 0m,
            ImporteIva = 21m,
            ImporteTributos = 0m,
            ImporteTotal = 121m,
            Receptor = new ReceptorComprobante
            {
                CodigoTipoDocumento = 80,
                NumeroDocumento = "30000000007",
                CondicionIva = CondicionIva.ResponsableInscripto,
            },
        };
        c.SubtotalesIva.Add(new SubtotalIva { CodigoAlicuotaAfip = 5, BaseImponible = 100m, Importe = 21m });

        var soap = Wsfev1RequestBuilder.FECAESolicitar(ta, "30000000099", c);

        soap.Should().Contain("<ar:CbteTipo>1</ar:CbteTipo>");
        soap.Should().Contain("<ar:PtoVta>1</ar:PtoVta>");
        soap.Should().Contain("<ar:CbteDesde>100</ar:CbteDesde>");
        soap.Should().Contain("<ar:CbteFch>20260428</ar:CbteFch>");
        soap.Should().Contain("<ar:ImpTotal>121.00</ar:ImpTotal>");
        soap.Should().Contain("<ar:ImpNeto>100.00</ar:ImpNeto>");
        soap.Should().Contain("<ar:ImpIVA>21.00</ar:ImpIVA>");
        soap.Should().Contain("<ar:MonId>DOL</ar:MonId>");
        soap.Should().Contain("<ar:CondicionIVAReceptorId>1</ar:CondicionIVAReceptorId>");
        soap.Should().Contain("<ar:Id>5</ar:Id>");
        soap.Should().Contain("<ar:FchServDesde>20260401</ar:FchServDesde>");
    }
}
