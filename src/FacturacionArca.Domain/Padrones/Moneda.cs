namespace FacturacionArca.Domain.Padrones;

public readonly record struct Moneda(string CodigoAfip, string Descripcion)
{
    public static readonly Moneda Pesos = new("PES", "Pesos Argentinos");
    public static readonly Moneda Dolar = new("DOL", "Dólar Estadounidense");
    public static readonly Moneda Euro = new("060", "Euro");
    public static readonly Moneda Real = new("012", "Real Brasileño");

    public static IReadOnlyList<Moneda> Conocidas { get; } = new[] { Pesos, Dolar, Euro, Real };

    public static Moneda? PorCodigo(string codigo) =>
        Conocidas.FirstOrDefault(m => string.Equals(m.CodigoAfip, codigo, StringComparison.OrdinalIgnoreCase))
            is { CodigoAfip: not null and not "" } m ? m : null;

    public bool EsLocal => CodigoAfip.Equals("PES", StringComparison.OrdinalIgnoreCase);
}
