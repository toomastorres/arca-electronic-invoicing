namespace FacturacionArca.Domain.Configuracion;

public sealed class ConfiguracionEmpresa
{
    public int Id { get; set; }
    public string Cuit { get; set; } = "";
    public string RazonSocial { get; set; } = "";
    public string NombreFantasia { get; set; } = "";
    public string DomicilioComercial { get; set; } = "";
    public string CondicionFrenteIva { get; set; } = "Responsable Inscripto";
    public DateOnly InicioActividades { get; set; }
    public string IngresosBrutos { get; set; } = "CM05";
    public bool EsConvenioMultilateral { get; set; } = true;
    public int PuntoVentaPorDefecto { get; set; } = 1;
    public ModoOperacion Modo { get; set; } = ModoOperacion.Homologacion;
    public string CertificadoPath { get; set; } = "";
    public string CertificadoPassword { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public string CbuPesos { get; set; } = "";
    public string CarpetaProformas { get; set; } = "";
    public string CarpetaProformasProcesadas { get; set; } = "";
    public string CarpetaArchivoFiscal { get; set; } = "";
    public string CarpetaPdfSalida { get; set; } = "";
    public string ImpresoraPorDefecto { get; set; } = "";
    public bool ImpresionSilenciosa { get; set; } = false;

    // FCE — Monto mínimo actualizable por el usuario (sin recompilar)
    public decimal MontoMinimoFce { get; set; } = 5_549_862m;
}

public enum ModoOperacion
{
    Homologacion = 0,
    Produccion = 1,
}
