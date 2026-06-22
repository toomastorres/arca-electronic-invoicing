namespace FacturacionArca.Domain.Wsaa;

public sealed class TicketAcceso
{
    public int Id { get; set; }
    public string Servicio { get; set; } = "wsfe";
    public string Token { get; set; } = "";
    public string Sign { get; set; } = "";
    public DateTime GenerationTime { get; set; }
    public DateTime ExpirationTime { get; set; }
    public string Cuit { get; set; } = "";
    public string Modo { get; set; } = "Homologacion";

    public bool EsVigente(DateTime ahoraUtc, TimeSpan margen) =>
        ExpirationTime - margen > ahoraUtc;
}
