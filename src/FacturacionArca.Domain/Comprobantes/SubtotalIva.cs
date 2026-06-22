using FacturacionArca.Domain.Padrones;

namespace FacturacionArca.Domain.Comprobantes;

public sealed class SubtotalIva
{
    public int Id { get; set; }
    public int CodigoAlicuotaAfip { get; set; }
    public decimal BaseImponible { get; set; }
    public decimal Importe { get; set; }

    public AlicuotaIva Alicuota => AlicuotaIva.PorCodigo(CodigoAlicuotaAfip)
        ?? throw new InvalidOperationException($"Alícuota AFIP desconocida: {CodigoAlicuotaAfip}");
}
