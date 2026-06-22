namespace FacturacionArca.Domain.Proformas;

public sealed class ProformaNapoles
{
    public int Id { get; set; }

    public string ArchivoOrigen { get; set; } = "";
    public string XmlCrudo { get; set; } = "";
    public DateTime FechaImportacion { get; set; } = DateTime.UtcNow;
    public EstadoProforma Estado { get; set; } = EstadoProforma.Pendiente;
    public int? ComprobanteId { get; set; }

    public string NumeroProforma { get; set; } = "";
    public int CodigoTipoComprobanteOrigen { get; set; }
    public DateOnly FechaEmision { get; set; }
    public int CodigoTipoDocumento { get; set; } = 80;
    public string NumeroDocumento { get; set; } = "";
    public string IvaCondicionTexto { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Domicilio { get; set; } = "";
    public string CondicionVentaTexto { get; set; } = "Contado";
    public string BuqueViaje { get; set; } = "";
    public string PorCuenta { get; set; } = "";
    public string Conocimiento { get; set; } = "";

    public decimal ImporteGravado { get; set; }
    public decimal ImporteNoGravado { get; set; }
    public decimal ImporteSubtotal { get; set; }
    public decimal ImporteIvaRi { get; set; }
    public decimal ImporteIvaRni { get; set; }
    public decimal ImporteTotal { get; set; }
    public string CodigoMoneda { get; set; } = "PES";
    public decimal CotizacionMoneda { get; set; } = 1m;

    public List<SubtotalIvaProforma> SubtotalesIva { get; set; } = new();
    public List<ItemProforma> Items { get; set; } = new();
    public ComprobanteAsociadoProforma? ComprobanteAsociado { get; set; }

    public string? UltimoErrorMensaje { get; set; }
    public DateTime? UltimoErrorFecha { get; set; }
}

public sealed class ComprobanteAsociadoProforma
{
    public int Id { get; set; }
    public int? CodigoTipoComprobante { get; set; }
    public int? NumeroPuntoVenta { get; set; }
    public long? NumeroComprobante { get; set; }
    public DateOnly? FechaEmision { get; set; }
    public string? CuitEmisor { get; set; }
}

public sealed class SubtotalIvaProforma
{
    public int Id { get; set; }
    public int Codigo { get; set; }
    public decimal Importe { get; set; }
}

public sealed class ItemProforma
{
    public int Id { get; set; }
    public int NumeroItem { get; set; }
    public string Referencias { get; set; } = "";
    public string DescripcionGrupo { get; set; } = "";
    public int UnidadesMtx { get; set; }
    public string CodigoMtx { get; set; } = "";
    public string CodigoItem { get; set; } = "";
    public string ItemDescripcion { get; set; } = "";
    public string ImpoExpo { get; set; } = "";
    public string TarifaDescripcion { get; set; } = "";
    public decimal PrecioUnitario { get; set; }
    public decimal Cantidad { get; set; }
    public int CodigoUnidadMedida { get; set; }
    public int CodigoCondicionIva { get; set; }
    public decimal ImporteIva { get; set; }
    public decimal ImporteItem { get; set; }
}
