namespace FacturacionArca.Domain.Comprobantes;

/// <summary>
/// Vincula un comprobante (típicamente NC/ND) a la factura original.
/// Se transmite a AFIP en el bloque CbtesAsoc del FECAESolicitar.
/// </summary>
public sealed class ComprobanteAsociado
{
    public int Id { get; set; }
    public int ComprobanteId { get; set; }

    /// <summary>Tipo AFIP del comprobante asociado (ej: 1=Factura A).</summary>
    public int CodigoTipoComprobante { get; set; }

    /// <summary>Punto de venta del comprobante asociado.</summary>
    public int PuntoVenta { get; set; }

    /// <summary>Número del comprobante asociado.</summary>
    public long Numero { get; set; }

    /// <summary>Fecha del comprobante asociado (opcional, requerido en algunos casos).</summary>
    public DateOnly? FechaEmision { get; set; }

    /// <summary>CUIT del emisor del comprobante asociado (cuando difiere del actual).</summary>
    public string? CuitEmisor { get; set; }
}
