namespace FacturacionArca.Domain.Errores;

public sealed record ArcaError(int Codigo, string MensajeOriginal)
{
    public string MensajeAmigable => ArcaErrorCatalog.MensajeAmigable(Codigo, MensajeOriginal);
}

public sealed class ArcaException : Exception
{
    public IReadOnlyList<ArcaError> Errores { get; }
    public string? RequestXml { get; }
    public string? ResponseXml { get; }

    public ArcaException(IEnumerable<ArcaError> errores, string? requestXml = null, string? responseXml = null)
        : base(string.Join(" | ", errores.Select(e => $"[{e.Codigo}] {e.MensajeAmigable}")))
    {
        Errores = errores.ToList();
        RequestXml = requestXml;
        ResponseXml = responseXml;
    }
}
