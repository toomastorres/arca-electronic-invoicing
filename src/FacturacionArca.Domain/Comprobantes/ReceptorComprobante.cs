namespace FacturacionArca.Domain.Comprobantes;

public sealed class ReceptorComprobante
{
    public int Id { get; set; }
    public int CodigoTipoDocumento { get; set; } = 80;
    public string NumeroDocumento { get; set; } = "";
    public string RazonSocial { get; set; } = "";
    public string Domicilio { get; set; } = "";
    public CondicionIva CondicionIva { get; set; } = CondicionIva.ConsumidorFinal;
    public string CondicionIvaTextoOriginal { get; set; } = "";
}
