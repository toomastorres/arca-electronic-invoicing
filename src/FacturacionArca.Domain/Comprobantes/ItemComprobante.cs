namespace FacturacionArca.Domain.Comprobantes;

public sealed class ItemComprobante
{
    public int Id { get; set; }
    public int NumeroItem { get; set; }
    public string CodigoItem { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string UnidadMedidaDescripcion { get; set; } = "";
    public int CodigoUnidadMedida { get; set; } = 7;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public int CodigoCondicionIvaNapoles { get; set; }
    public int CodigoAlicuotaAfip { get; set; }
    public decimal ImporteIva { get; set; }
    public decimal ImporteItem { get; set; }
    public string Referencias { get; set; } = "";
    /// <summary>Base de tarifa del ítem (ej: "As Agreed", "Per Unit"). Columna "Rate Basis" del layout del ERP de origen.</summary>
    public string TarifaDescripcion { get; set; } = "";
    /// <summary>Tipo de cargo visible (ej: "Local", "Export"). Columna "Charge Type" del layout del ERP de origen.</summary>
    public string TipoCargoDescripcion { get; set; } = "";
}
