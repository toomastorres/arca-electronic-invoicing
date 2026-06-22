namespace FacturacionArca.Domain.Comprobantes;

public enum TipoComprobante
{
    FacturaA = 1,
    NotaDebitoA = 2,
    NotaCreditoA = 3,
    ReciboA = 4,
    NotaVentaContadoA = 5,

    FacturaB = 6,
    NotaDebitoB = 7,
    NotaCreditoB = 8,
    ReciboB = 9,
    NotaVentaContadoB = 10,

    FacturaC = 11,
    NotaDebitoC = 12,
    NotaCreditoC = 13,
    ReciboC = 15,

    FacturaCreditoMipymeA = 201,
    NotaDebitoElectronicaMipymeA = 202,
    NotaCreditoElectronicaMipymeA = 203,

    FacturaCreditoMipymeB = 206,
    NotaDebitoElectronicaMipymeB = 207,
    NotaCreditoElectronicaMipymeB = 208,

    FacturaCreditoMipymeC = 211,
    NotaDebitoElectronicaMipymeC = 212,
    NotaCreditoElectronicaMipymeC = 213,
}

public static class TipoComprobanteExtensions
{
    public static bool EsFacturaA(this TipoComprobante t) =>
        t is TipoComprobante.FacturaA or TipoComprobante.NotaDebitoA or TipoComprobante.NotaCreditoA
          or TipoComprobante.FacturaCreditoMipymeA or TipoComprobante.NotaDebitoElectronicaMipymeA
          or TipoComprobante.NotaCreditoElectronicaMipymeA;

    public static bool EsFacturaB(this TipoComprobante t) =>
        t is TipoComprobante.FacturaB or TipoComprobante.NotaDebitoB or TipoComprobante.NotaCreditoB
          or TipoComprobante.FacturaCreditoMipymeB or TipoComprobante.NotaDebitoElectronicaMipymeB
          or TipoComprobante.NotaCreditoElectronicaMipymeB;

    public static bool EsFce(this TipoComprobante t) => (int)t >= 201 && (int)t <= 213;

    public static bool EsNotaCredito(this TipoComprobante t) =>
        t is TipoComprobante.NotaCreditoA or TipoComprobante.NotaCreditoB or TipoComprobante.NotaCreditoC
          or TipoComprobante.NotaCreditoElectronicaMipymeA or TipoComprobante.NotaCreditoElectronicaMipymeB
          or TipoComprobante.NotaCreditoElectronicaMipymeC;

    public static bool EsNotaDebito(this TipoComprobante t) =>
        t is TipoComprobante.NotaDebitoA or TipoComprobante.NotaDebitoB or TipoComprobante.NotaDebitoC
          or TipoComprobante.NotaDebitoElectronicaMipymeA or TipoComprobante.NotaDebitoElectronicaMipymeB
          or TipoComprobante.NotaDebitoElectronicaMipymeC;

    public static string LetraVisible(this TipoComprobante t) =>
        t.EsFacturaA() ? "A" : t.EsFacturaB() ? "B" : "C";

    /// <summary>
    /// Devuelve el tipo de la "factura origen" típica para una NC/ND
    /// (ej: NCA → FacturaA, NCB → FacturaB). Para facturas, devuelve el mismo tipo.
    /// </summary>
    public static TipoComprobante TipoFacturaOrigen(this TipoComprobante t) => t switch
    {
        TipoComprobante.NotaCreditoA or TipoComprobante.NotaDebitoA => TipoComprobante.FacturaA,
        TipoComprobante.NotaCreditoB or TipoComprobante.NotaDebitoB => TipoComprobante.FacturaB,
        TipoComprobante.NotaCreditoC or TipoComprobante.NotaDebitoC => TipoComprobante.FacturaC,
        TipoComprobante.NotaCreditoElectronicaMipymeA or TipoComprobante.NotaDebitoElectronicaMipymeA => TipoComprobante.FacturaCreditoMipymeA,
        TipoComprobante.NotaCreditoElectronicaMipymeB or TipoComprobante.NotaDebitoElectronicaMipymeB => TipoComprobante.FacturaCreditoMipymeB,
        TipoComprobante.NotaCreditoElectronicaMipymeC or TipoComprobante.NotaDebitoElectronicaMipymeC => TipoComprobante.FacturaCreditoMipymeC,
        _ => t,
    };

    public static bool RequiereAsociado(this TipoComprobante t) =>
        t.EsNotaCredito() || t.EsNotaDebito();

    /// <summary>true si el tipo es específicamente una Factura FCE (no NC ni ND).</summary>
    public static bool EsFacturaFce(this TipoComprobante t) =>
        t is TipoComprobante.FacturaCreditoMipymeA
        or TipoComprobante.FacturaCreditoMipymeB
        or TipoComprobante.FacturaCreditoMipymeC;

    /// <summary>
    /// Mapea un tipo de comprobante normal a su equivalente FCE MiPyME.
    /// Ej: FacturaA (1) → FacturaCreditoMipymeA (201).
    /// Si ya es FCE, devuelve el mismo valor.
    /// </summary>
    public static TipoComprobante? EquivalenteFce(this TipoComprobante t) => t switch
    {
        TipoComprobante.FacturaA => TipoComprobante.FacturaCreditoMipymeA,
        TipoComprobante.NotaDebitoA => TipoComprobante.NotaDebitoElectronicaMipymeA,
        TipoComprobante.NotaCreditoA => TipoComprobante.NotaCreditoElectronicaMipymeA,
        TipoComprobante.FacturaB => TipoComprobante.FacturaCreditoMipymeB,
        TipoComprobante.NotaDebitoB => TipoComprobante.NotaDebitoElectronicaMipymeB,
        TipoComprobante.NotaCreditoB => TipoComprobante.NotaCreditoElectronicaMipymeB,
        TipoComprobante.FacturaC => TipoComprobante.FacturaCreditoMipymeC,
        TipoComprobante.NotaDebitoC => TipoComprobante.NotaDebitoElectronicaMipymeC,
        TipoComprobante.NotaCreditoC => TipoComprobante.NotaCreditoElectronicaMipymeC,
        _ when t.EsFce() => t, // Ya es FCE
        _ => null, // Tipo no mapeado (Recibo, NVContado, etc.)
    };
}
