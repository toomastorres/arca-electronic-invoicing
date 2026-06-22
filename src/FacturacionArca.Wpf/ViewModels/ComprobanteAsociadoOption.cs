using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Wpf.ViewModels;

/// <summary>
/// Representación visual ligera de un comprobante para el combo NC↔Factura.
/// </summary>
public sealed class ComprobanteAsociadoOption
{
    public int CodigoTipoComprobante { get; init; }
    public int PuntoVenta { get; init; }
    public long Numero { get; init; }
    public DateOnly? FechaEmision { get; init; }
    public string? CuitEmisor { get; init; }
    public string Display { get; init; } = "";

    public override string ToString() => Display;

    public static ComprobanteAsociadoOption DesdeComprobante(Comprobante c) => new()
    {
        CodigoTipoComprobante = (int)c.Tipo,
        PuntoVenta = c.PuntoVenta,
        Numero = c.Numero ?? 0,
        FechaEmision = c.FechaEmision,
        CuitEmisor = null, // mismo emisor → no se transmite
        Display = $"{c.Tipo.LetraVisible()} {c.PuntoVenta:D4}-{(c.Numero ?? 0):D8}  ·  {c.FechaEmision:dd/MM/yyyy}  ·  ${c.ImporteTotal:N2} {c.CodigoMoneda}",
    };

    public static ComprobanteAsociadoOption DesdeProforma(int tipo, int ptoVta, long nro, DateOnly? fecha, string? cuit) => new()
    {
        CodigoTipoComprobante = tipo,
        PuntoVenta = ptoVta,
        Numero = nro,
        FechaEmision = fecha,
        CuitEmisor = cuit,
        Display = $"[XML] Tipo {tipo:D2} {ptoVta:D4}-{nro:D8}  ·  {(fecha?.ToString("dd/MM/yyyy") ?? "—")}",
    };

    public static ComprobanteAsociadoOption Manual { get; } = new()
    {
        CodigoTipoComprobante = 0,
        PuntoVenta = 0,
        Numero = 0,
        Display = "— Ingreso manual —",
    };
}
