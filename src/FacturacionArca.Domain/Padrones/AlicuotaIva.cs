namespace FacturacionArca.Domain.Padrones;

public readonly record struct AlicuotaIva(int CodigoAfip, decimal Porcentaje, string Descripcion)
{
    public static readonly AlicuotaIva NoGravado = new(3, 0m, "0%");
    public static readonly AlicuotaIva Reducida105 = new(4, 10.5m, "10,5%");
    public static readonly AlicuotaIva General21 = new(5, 21m, "21%");
    public static readonly AlicuotaIva Aumentada27 = new(6, 27m, "27%");
    public static readonly AlicuotaIva Reducida5 = new(8, 5m, "5%");
    public static readonly AlicuotaIva Reducida25 = new(9, 2.5m, "2,5%");

    public static IReadOnlyList<AlicuotaIva> Todas { get; } = new[]
    {
        NoGravado, Reducida105, General21, Aumentada27, Reducida5, Reducida25,
    };

    public static AlicuotaIva? PorCodigo(int codigo) =>
        Todas.FirstOrDefault(a => a.CodigoAfip == codigo) is { CodigoAfip: not 0 } a ? a : null;
}
