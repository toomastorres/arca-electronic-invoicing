namespace FacturacionArca.Domain.Comprobantes;

public enum CondicionIva
{
    ResponsableInscripto = 1,
    ResponsableNoInscripto = 2,
    NoResponsable = 3,
    Exento = 4,
    ConsumidorFinal = 5,
    Monotributo = 6,
    SujetoNoCategorizado = 7,
    ProveedorDelExterior = 8,
    ClienteDelExterior = 9,
    IvaLiberadoLey19640 = 10,
    AgenteDePercepcion = 11,
    PequenoContribuyenteEventual = 12,
    MonotributistaSocialP = 13,
    PequenoContribuyenteEventualSocial = 14,
}

public static class CondicionIvaParser
{
    public static CondicionIva FromTextoNapoles(string? texto) =>
        (texto ?? "").Trim().ToUpperInvariant() switch
        {
            "RESPONSABLE INSCRIPTO" or "RI"            => CondicionIva.ResponsableInscripto,
            "MONOTRIBUTO" or "MONOTRIBUTISTA" or "MT"  => CondicionIva.Monotributo,
            "EXENTO" or "EX"                           => CondicionIva.Exento,
            "CONSUMIDOR FINAL" or "CF"                 => CondicionIva.ConsumidorFinal,
            "NO RESPONSABLE"                           => CondicionIva.NoResponsable,
            "PROVEEDOR DEL EXTERIOR"                   => CondicionIva.ProveedorDelExterior,
            "CLIENTE DEL EXTERIOR"                     => CondicionIva.ClienteDelExterior,
            "IVA LIBERADO" or "IVA LIBERADO LEY 19640" => CondicionIva.IvaLiberadoLey19640,
            ""                                         => CondicionIva.ConsumidorFinal,
            _                                          => CondicionIva.ConsumidorFinal,
        };
}
