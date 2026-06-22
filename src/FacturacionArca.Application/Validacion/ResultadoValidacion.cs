namespace FacturacionArca.Application.Validacion;

public sealed record ErrorValidacion(string Campo, string Mensaje, int? CodigoArca = null);

public sealed class ResultadoValidacion
{
    private readonly List<ErrorValidacion> _errores = new();
    public IReadOnlyList<ErrorValidacion> Errores => _errores;
    public bool EsValido => _errores.Count == 0;

    public ResultadoValidacion AgregarError(string campo, string mensaje, int? codigoArca = null)
    {
        _errores.Add(new ErrorValidacion(campo, mensaje, codigoArca));
        return this;
    }

    public void Combinar(ResultadoValidacion otro)
    {
        _errores.AddRange(otro._errores);
    }

    public string ResumenAmigable => string.Join(Environment.NewLine,
        _errores.Select(e => e.CodigoArca is { } c ? $"[{c}] {e.Mensaje}" : e.Mensaje));
}
