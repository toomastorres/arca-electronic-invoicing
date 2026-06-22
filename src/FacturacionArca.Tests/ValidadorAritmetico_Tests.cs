using Xunit;
using FacturacionArca.Application.Validacion;
using FacturacionArca.Domain.Comprobantes;
using FluentAssertions;

namespace FacturacionArca.Tests;

public class ValidadorAritmetico_Tests
{
    private static Comprobante CrearComprobante(decimal neto, decimal noGravado, decimal iva, decimal tributos, decimal total)
    {
        var c = new Comprobante
        {
            Tipo = TipoComprobante.FacturaA,
            ImporteNeto = neto,
            ImporteNoGravado = noGravado,
            ImporteExento = 0m,
            ImporteIva = iva,
            ImporteTributos = tributos,
            ImporteTotal = total,
        };
        if (iva > 0)
            c.SubtotalesIva.Add(new SubtotalIva { CodigoAlicuotaAfip = 5, BaseImponible = neto, Importe = iva });
        return c;
    }

    [Fact]
    public void Valida_CuandoSumaCuadra()
    {
        var c = CrearComprobante(neto: 100m, noGravado: 50m, iva: 21m, tributos: 0m, total: 171m);
        var v = new ValidadorAritmeticoAfip().Validar(c);
        v.EsValido.Should().BeTrue();
    }

    [Fact]
    public void Falla_CuandoTotalNoCoincide()
    {
        var c = CrearComprobante(neto: 100m, noGravado: 0m, iva: 21m, tributos: 0m, total: 200m);
        var v = new ValidadorAritmeticoAfip().Validar(c);
        v.EsValido.Should().BeFalse();
        v.Errores.Should().Contain(e => e.CodigoArca == 10048);
    }

    [Fact]
    public void Tolera_DiferenciasDeRedondeo()
    {
        var c = CrearComprobante(neto: 100m, noGravado: 0m, iva: 21m, tributos: 0m, total: 121.005m);
        var v = new ValidadorAritmeticoAfip().Validar(c);
        v.EsValido.Should().BeTrue();
    }

    [Fact]
    public void Falla_CuandoBaseImponibleIvaNoCoincideConNeto()
    {
        var c = CrearComprobante(neto: 100m, noGravado: 0m, iva: 21m, tributos: 0m, total: 121m);
        c.SubtotalesIva.Clear();
        c.SubtotalesIva.Add(new SubtotalIva { CodigoAlicuotaAfip = 5, BaseImponible = 50m, Importe = 21m });
        var v = new ValidadorAritmeticoAfip().Validar(c);
        v.EsValido.Should().BeFalse();
        v.Errores.Should().Contain(e => e.CodigoArca == 10061);
    }
}
