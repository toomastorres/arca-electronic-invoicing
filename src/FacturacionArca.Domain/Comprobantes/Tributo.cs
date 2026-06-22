namespace FacturacionArca.Domain.Comprobantes;

public sealed class Tributo
{
    public int Id { get; set; }
    public int IdAfip { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal BaseImponible { get; set; }
    public decimal Alicuota { get; set; }
    public decimal Importe { get; set; }

    public const int IdImpuestoNacional = 1;
    public const int IdImpuestoProvincial = 2;
    public const int IdImpuestoMunicipal = 3;
    public const int IdImpuestoInterno = 4;
    public const int IdIngresosBrutos = 5;
    public const int IdImpuestosParaMunicipales = 6;
    public const int IdOtros = 99;
}
