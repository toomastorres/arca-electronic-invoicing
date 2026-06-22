using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Infrastructure.Pdf;
using Xunit;

namespace FacturacionArca.Tests;

public class PdfGenerationDemo
{
    class MockConfigRepo : IConfiguracionRepository
    {
        public Task<ConfiguracionEmpresa> GetAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new ConfiguracionEmpresa
            {
                RazonSocial = "ATLANTIC MARITIME AGENCY S.A.",
                DomicilioComercial = "Av. del Puerto 1000 - 1° Piso (C1002ABP) - Tel.:4000-0000\nBuenos Aires - Argentina",
                CondicionFrenteIva = "I.V.A. RESPONSABLE INSCRIPTO",
                Cuit = "30-71111111-7",
                IngresosBrutos = "30-71111111-7",
                InicioActividades = new DateOnly(2008, 8, 1),
                CbuPesos = "0110000000000000000001"
            });
        }
        public Task SaveAsync(ConfiguracionEmpresa config, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Generar_Ejemplo_PDF()
    {
        // 1. Arrange Config
        var cfgRepo = new MockConfigRepo();

        var qrBuilder = new QrAfipBuilder();
        var renderer = new MigraDocPdfRenderer(qrBuilder, cfgRepo);

        // 2. Arrange Comprobante
        var c = new Comprobante
        {
            Tipo = TipoComprobante.FacturaB,
            PuntoVenta = 4,
            Numero = 2438,
            FechaEmision = new DateOnly(2026, 4, 30),
            NumeroProforma = "2026T0058132",
            CondicionVenta = CondicionVenta.Contado,
            CodigoMoneda = "DOL",
            CotizacionMoneda = 1000.0000m,
            BuqueViaje = "ATLANTIC STAR 0226",
            PorCuenta = "Atlantic Maritime Agency",
            Conocimiento = "S3-99990003",
            // Montos ILUSTRATIVOS (no son valores originales).
            ImporteNeto = 1200.00m,
            ImporteNoGravado = 0m,
            ImporteTotal = 1200.00m,
            Receptor = new ReceptorComprobante
            {
                RazonSocial = "DEMO PAPER PACKAGING S.A.",
                Domicilio = "AV. DEMO 300 C1002ABP C.A.B.A. ARGENTINA",
                CondicionIva = CondicionIva.ConsumidorFinal,
                NumeroDocumento = "55000000016"
            },
            Cae = new Cae("00000000000000", new DateOnly(2026, 5, 10), DateTime.UtcNow)
        };

        c.Items.Add(new ItemComprobante
        {
            Descripcion = "40 ft. High Cube Paper Rolls (ACLU9674543...)",
            TipoCargoDescripcion = "40 Ft Cntr Sweeping",
            TarifaDescripcion = "100.00",
            PrecioUnitario = 100.00m,
            Cantidad = 8.000m,
            ImporteItem = 800.00m
        });
        
        c.Items.Add(new ItemComprobante
        {
            Descripcion = "40 ft. High Cube Paper Rolls (ACLU9674543...)",
            TipoCargoDescripcion = "40 Ft Cntr Sealing Cost",
            TarifaDescripcion = "50.00",
            PrecioUnitario = 50.00m,
            Cantidad = 8.000m,
            ImporteItem = 400.00m
        });

        // 3. Act
        var outDir = Path.Combine(Directory.GetCurrentDirectory(), "PdfSalida");
        await renderer.RenderizarYGuardarAsync(c, outDir);
    }
}
