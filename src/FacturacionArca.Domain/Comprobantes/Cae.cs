namespace FacturacionArca.Domain.Comprobantes;

public sealed record Cae(string Numero, DateOnly FechaVencimiento, DateTime FechaProceso)
{
    public bool EsValido => !string.IsNullOrWhiteSpace(Numero) && Numero.Length == 14;
}
