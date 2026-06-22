namespace FacturacionArca.Domain.Padrones;

/// <summary>
/// Entrada del padrón AGIP IIBB CABA (publicado mensualmente como ARDJUYYYYMMDD.TXT).
/// Formato: fechaPublicacion;vigDesde;vigHasta;cuit;tipoCont;flag1;flag2;alicPercep;alicReten;regPercep;regReten;razonSocial
/// </summary>
public sealed class PadronIibbCaba
{
    public int Id { get; set; }

    /// <summary>CUIT del contribuyente (11 dígitos sin guiones).</summary>
    public string Cuit { get; set; } = "";

    /// <summary>Fecha publicación del padrón (cuando AGIP lo emitió).</summary>
    public DateOnly FechaPublicacion { get; set; }

    /// <summary>Fecha desde la cual este padrón es vigente.</summary>
    public DateOnly VigenciaDesde { get; set; }

    /// <summary>Fecha hasta la cual este padrón es vigente.</summary>
    public DateOnly VigenciaHasta { get; set; }

    /// <summary>Tipo de contribuyente: D=Local CABA, S=Convenio Multilateral, etc.</summary>
    public string TipoContribuyente { get; set; } = "";

    /// <summary>Flag adicional 1 (Sujeto a percepción).</summary>
    public string Flag1 { get; set; } = "";

    /// <summary>Flag adicional 2 (Sujeto a retención).</summary>
    public string Flag2 { get; set; } = "";

    /// <summary>Alícuota porcentual de percepción (ej: 3.50 = 3.50%).</summary>
    public decimal AlicuotaPercepcion { get; set; }

    /// <summary>Alícuota porcentual de retención.</summary>
    public decimal AlicuotaRetencion { get; set; }

    /// <summary>Régimen de percepción (código AGIP, normalmente 2 dígitos).</summary>
    public string RegimenPercepcion { get; set; } = "";

    /// <summary>Régimen de retención (código AGIP).</summary>
    public string RegimenRetencion { get; set; } = "";

    /// <summary>Razón social tal como figura en el padrón.</summary>
    public string RazonSocial { get; set; } = "";
}
