namespace FacturacionArca.Domain.Comprobantes;

public sealed class Comprobante
{
    public int Id { get; set; }

    public string ProformaOrigen { get; set; } = "";
    /// <summary>Número de proforma Nápoles (ej: "2026T0058088"). Se muestra como "Pf.:" en el PDF.</summary>
    public string NumeroProforma { get; set; } = "";

    public TipoComprobante Tipo { get; set; }
    public Concepto Concepto { get; set; } = Concepto.ProductosYServicios;
    public CondicionVenta CondicionVenta { get; set; } = CondicionVenta.Contado;

    public int PuntoVenta { get; set; }
    public long? Numero { get; set; }
    public DateOnly FechaEmision { get; set; }
    public DateOnly? FechaServicioDesde { get; set; }
    public DateOnly? FechaServicioHasta { get; set; }
    public DateOnly? FechaVencimientoPago { get; set; }

    public ReceptorComprobante Receptor { get; set; } = new();

    public string CodigoMoneda { get; set; } = "PES";
    public decimal CotizacionMoneda { get; set; } = 1m;

    public decimal ImporteNeto { get; set; }
    public decimal ImporteNoGravado { get; set; }
    public decimal ImporteExento { get; set; }
    public decimal ImporteIva { get; set; }
    public decimal ImporteTributos { get; set; }
    public decimal ImporteTotal { get; set; }

    public List<ItemComprobante> Items { get; set; } = new();
    public List<SubtotalIva> SubtotalesIva { get; set; } = new();
    public List<Tributo> Tributos { get; set; } = new();
    public List<ComprobanteAsociado> ComprobantesAsociados { get; set; } = new();

    public string CondicionIngresosBrutosSeleccionada { get; set; } = "Convenio Multilateral";

    public string BuqueViaje { get; set; } = "";
    public string PorCuenta { get; set; } = "";
    public string Conocimiento { get; set; } = "";

    public Cae? Cae { get; set; }
    public string? RespuestaArcaXml { get; set; }
    public string? PdfPath { get; set; }

    // ── Política de saldo cta cte solicitada por el cliente (no se transmite a ARCA) ──
    /// <summary>Tipo P = pesificada (total fijo en ARS al CAE) | Tipo S = saldos (en moneda original).</summary>
    public TipoSaldoCtaCte TipoSaldo { get; set; } = TipoSaldoCtaCte.Pesificado;

    /// <summary>
    /// Total congelado en ARS al obtener el CAE = ImporteTotal × CotizacionMoneda.
    /// Para tipo P queda como dato fiscal-financiero; para tipo S queda como referencia histórica.
    /// </summary>
    public decimal SaldoArsFijado { get; set; }

    /// <summary>"PES" para tipo P; código original (DOL/EUR/...) para tipo S.</summary>
    public string SaldoMoneda { get; set; } = "PES";

    // ── FCE MiPyMEs específico ──
    /// <summary>true=SCA (Sistema Circulación Abierta, Opcional 27=S); false=ADC (Agente Depósito Colectivo, Opcional 27=N); null=N/A.</summary>
    public bool? EsSCA { get; set; }
    /// <summary>CBU del beneficiario (FCE — Opcional 2101 si SCA).</summary>
    public string? CbuFce { get; set; }
    /// <summary>Alias bancario (FCE — Opcional 2102).</summary>
    public string? AliasFce { get; set; }

    public string? Observaciones { get; set; }

    public DateTime FechaImportacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEmisionEfectiva { get; set; }
}
