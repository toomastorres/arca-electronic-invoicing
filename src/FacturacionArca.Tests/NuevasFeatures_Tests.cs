using Xunit;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Application.UseCases;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Domain.Padrones;
using FacturacionArca.Domain.Proformas;
using FacturacionArca.Domain.Wsaa;
using FacturacionArca.Infrastructure.Arca.Wsfev1;
using FacturacionArca.Infrastructure.Padrones;
using FacturacionArca.Infrastructure.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FacturacionArca.Tests;

public class NuevasFeatures_Tests
{
    [Fact]
    public void NumeroEnLetras_Convierte720564()
    {
        var resultado = NumeroEnLetras.Convertir(720564m, "Pesos");
        resultado.Should().Be("SETECIENTOS VEINTE MIL QUINIENTOS SESENTA Y CUATRO PESOS CON 00 CENTAVOS");
    }

    [Fact]
    public void NumeroEnLetras_ConvierteUnPesoSingular()
    {
        var resultado = NumeroEnLetras.Convertir(1m, "Pesos");
        resultado.Should().Be("UN PESO CON 00 CENTAVOS");
    }

    [Fact]
    public void NumeroEnLetras_ConvierteCero()
    {
        var resultado = NumeroEnLetras.Convertir(0m, "Pesos");
        resultado.Should().Be("CERO PESOS CON 00 CENTAVOS");
    }

    [Fact]
    public void NumeroEnLetras_ConvierteCentavosCorrectamente()
    {
        var resultado = NumeroEnLetras.Convertir(1234.56m, "Pesos");
        resultado.Should().Contain("MIL DOSCIENTOS TREINTA Y CUATRO");
        resultado.Should().Contain("CON 56 CENTAVOS");
    }

    [Fact]
    public void Wsfev1Builder_IncluyeCbtesAsoc_EnNotaCredito()
    {
        var ta = new TicketAcceso { Token = "T", Sign = "S" };
        var c = new Comprobante
        {
            Tipo = TipoComprobante.NotaCreditoA,
            Concepto = Concepto.Servicios,
            PuntoVenta = 4,
            Numero = 100,
            FechaEmision = new DateOnly(2026, 4, 1),
            FechaServicioDesde = new DateOnly(2026, 4, 1),
            FechaServicioHasta = new DateOnly(2026, 4, 1),
            FechaVencimientoPago = new DateOnly(2026, 4, 30),
            CodigoMoneda = "PES",
            CotizacionMoneda = 1m,
            ImporteNeto = -100m,
            ImporteIva = -21m,
            ImporteTotal = -121m,
            Receptor = new ReceptorComprobante
            {
                CodigoTipoDocumento = 80,
                NumeroDocumento = "30000000007",
                CondicionIva = CondicionIva.ResponsableInscripto,
            },
        };
        c.SubtotalesIva.Add(new SubtotalIva { CodigoAlicuotaAfip = 5, BaseImponible = -100m, Importe = -21m });
        c.ComprobantesAsociados.Add(new ComprobanteAsociado
        {
            CodigoTipoComprobante = 1,
            PuntoVenta = 4,
            Numero = 50,
            FechaEmision = new DateOnly(2026, 3, 15),
        });

        var soap = Wsfev1RequestBuilder.FECAESolicitar(ta, "30000000099", c);

        soap.Should().Contain("<ar:CbtesAsoc>");
        soap.Should().Contain("<ar:Tipo>1</ar:Tipo>");
        soap.Should().Contain("<ar:PtoVta>4</ar:PtoVta>");
        soap.Should().Contain("<ar:Nro>50</ar:Nro>");
        soap.Should().Contain("<ar:CbteFch>20260315</ar:CbteFch>");
    }

    [Fact]
    public void PadronAgipParser_ParseaLineaValida()
    {
        var linea = "26032026;01042026;30042026;20111111112;D;S;N;3,50;0,00;06;00;CLIENTE DEMO";
        var entry = PadronAgipParser.ParseLine(linea);

        entry.Should().NotBeNull();
        entry!.Cuit.Should().Be("20111111112");
        entry.FechaPublicacion.Should().Be(new DateOnly(2026, 3, 26));
        entry.VigenciaDesde.Should().Be(new DateOnly(2026, 4, 1));
        entry.VigenciaHasta.Should().Be(new DateOnly(2026, 4, 30));
        entry.AlicuotaPercepcion.Should().Be(3.50m);
        entry.RegimenPercepcion.Should().Be("06");
        entry.RazonSocial.Should().Be("CLIENTE DEMO");
    }

    [Fact]
    public void PadronAgipParser_LineaCorta_DevuelveNull()
    {
        var entry = PadronAgipParser.ParseLine("uno;dos;tres");
        entry.Should().BeNull();
    }

    [Fact]
    public async Task CalcularPercepcionIibbCaba_DeBaseImponibleYAlicuotaPadron()
    {
        var fakeRepo = new FakePadronRepo();
        fakeRepo.Insertar(new PadronIibbCaba
        {
            Cuit = "30580913446",
            VigenciaDesde = new DateOnly(2026, 1, 1),
            VigenciaHasta = new DateOnly(2026, 12, 31),
            FechaPublicacion = new DateOnly(2026, 1, 1),
            AlicuotaPercepcion = 3m,
            RegimenPercepcion = "06",
        });

        var calc = new CalcularPercepcionIibbCaba(fakeRepo);
        var r = await calc.EjecutarAsync("30580913446", 581100m, new DateOnly(2026, 4, 29));

        r.Aplica.Should().BeTrue();
        r.Alicuota.Should().Be(3m);
        r.BaseImponible.Should().Be(581100m);
        r.Importe.Should().Be(17433m); // 581100 * 3% = 17433
        r.RegimenAgip.Should().Be("06");
    }

    [Fact]
    public async Task CalcularPercepcionIibbCaba_CuitFueraDelPadron_NoAplica()
    {
        var calc = new CalcularPercepcionIibbCaba(new FakePadronRepo());
        var r = await calc.EjecutarAsync("99999999999", 1000m);
        r.Aplica.Should().BeFalse();
        r.Importe.Should().Be(0m);
    }

    [Fact]
    public void MapearProformaAComprobante_AgregaAsociado_AOComprobante()
    {
        var p = new ProformaNapoles
        {
            NumeroProforma = "2026T0058088",
            CodigoTipoComprobanteOrigen = (int)TipoComprobante.NotaCreditoA,
            FechaEmision = new DateOnly(2026, 4, 29),
            CodigoTipoDocumento = 80,
            NumeroDocumento = "30580913446",
            Cliente = "CINTRA SRL",
            CodigoMoneda = "PES",
            CotizacionMoneda = 1m,
            ImporteGravado = 100m,
            ImporteTotal = 121m,
            ImporteIvaRi = 21m,
            IvaCondicionTexto = "RESPONSABLE INSCRIPTO",
        };
        var cfg = new ConfiguracionEmpresa { PuntoVentaPorDefecto = 4, Cuit = "30711111117" };
        var opciones = new OpcionesMapeo
        {
            CondicionIngresosBrutos = "Convenio Multilateral",
            ComprobanteAsociado = new ComprobanteAsociadoMapeo
            {
                CodigoTipoComprobante = (int)TipoComprobante.FacturaA,
                PuntoVenta = 4,
                Numero = 38594,
                FechaEmision = new DateOnly(2026, 4, 29),
            },
        };

        var c = new MapearProformaAComprobante().Ejecutar(p, cfg, opciones);

        c.ComprobantesAsociados.Should().HaveCount(1);
        c.ComprobantesAsociados[0].CodigoTipoComprobante.Should().Be(1);
        c.ComprobantesAsociados[0].PuntoVenta.Should().Be(4);
        c.ComprobantesAsociados[0].Numero.Should().Be(38594);
    }

    [Fact]
    public async Task EscribirCallbackOut_GeneraXmlConFormatoEsperado()
    {
        var c = new Comprobante
        {
            NumeroProforma = "2020T0000090",
            Tipo = TipoComprobante.FacturaA,
            PuntoVenta = 4,
            Numero = 8380,
            FechaEmision = new DateOnly(2020, 1, 30),
            Cae = new Cae("70053815341741", new DateOnly(2020, 2, 9), new DateTime(2020, 1, 30, 10, 0, 0)),
        };
        c.Tributos.Add(new Tributo
        {
            IdAfip = Tributo.IdIngresosBrutos,
            Descripcion = "Percep.IIBB CABA",
            BaseImponible = 17810m,
            Alicuota = 1.5m,
            Importe = 267.15m,
        });

        var tmp = Path.Combine(Path.GetTempPath(), "fa_callback_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var uc = new EscribirCallbackOut(NullLogger<EscribirCallbackOut>.Instance);
            var path = await uc.EjecutarAsync(c, tmp);

            path.Should().NotBeNull();
            File.Exists(path).Should().BeTrue();
            var contenido = await File.ReadAllTextAsync(path!);

            contenido.Should().Contain("<Proforma>2020T0000090</Proforma>");
            contenido.Should().Contain("<CodigoTipoComprobante>01 </CodigoTipoComprobante>");
            contenido.Should().Contain("<NumeroPuntoventa>0004</NumeroPuntoventa>");
            contenido.Should().Contain("<NumeroComprobante>00008380</NumeroComprobante>");
            contenido.Should().Contain("<Caenumero>70053815341741</Caenumero>");
            contenido.Should().Contain("<Caefecha>20200130</Caefecha>");
            contenido.Should().Contain("<Peribcaba>267.15</Peribcaba>");
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        }
    }

    /// <summary>Repositorio en memoria para testear CalcularPercepcionIibbCaba sin EF.</summary>
    private sealed class FakePadronRepo : IPadronIibbRepository
    {
        private readonly List<PadronIibbCaba> _entries = new();

        public void Insertar(PadronIibbCaba e) => _entries.Add(e);

        public Task<int> ReemplazarLoteAsync(DateOnly fechaPublicacion, IAsyncEnumerable<PadronIibbCaba> entradas, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<PadronIibbCaba?> BuscarVigenteAsync(string cuit, DateOnly? a = null, CancellationToken ct = default)
        {
            var fecha = a ?? DateOnly.FromDateTime(DateTime.Today);
            return Task.FromResult(_entries.FirstOrDefault(e =>
                e.Cuit == cuit && e.VigenciaDesde <= fecha && e.VigenciaHasta >= fecha));
        }

        public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_entries.Count);
        public Task<DateOnly?> UltimaFechaPublicacionAsync(CancellationToken ct = default) =>
            Task.FromResult<DateOnly?>(_entries.Count == 0 ? null : _entries.Max(e => e.FechaPublicacion));
    }
}
