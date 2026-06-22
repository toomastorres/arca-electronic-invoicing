namespace FacturacionArca.Domain.Padrones;

public sealed class Jurisdiccion
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal AlicuotaPercepcionIIBB { get; set; }

    public static IReadOnlyList<Jurisdiccion> Predefinidas { get; } = new[]
    {
        new Jurisdiccion { Codigo = "CABA", Nombre = "Ciudad Autónoma de Buenos Aires" },
        new Jurisdiccion { Codigo = "BSAS", Nombre = "Buenos Aires" },
        new Jurisdiccion { Codigo = "CAT",  Nombre = "Catamarca" },
        new Jurisdiccion { Codigo = "CHA",  Nombre = "Chaco" },
        new Jurisdiccion { Codigo = "CHU",  Nombre = "Chubut" },
        new Jurisdiccion { Codigo = "COR",  Nombre = "Córdoba" },
        new Jurisdiccion { Codigo = "CRR",  Nombre = "Corrientes" },
        new Jurisdiccion { Codigo = "ER",   Nombre = "Entre Ríos" },
        new Jurisdiccion { Codigo = "FOR",  Nombre = "Formosa" },
        new Jurisdiccion { Codigo = "JUJ",  Nombre = "Jujuy" },
        new Jurisdiccion { Codigo = "LP",   Nombre = "La Pampa" },
        new Jurisdiccion { Codigo = "LR",   Nombre = "La Rioja" },
        new Jurisdiccion { Codigo = "MZA",  Nombre = "Mendoza" },
        new Jurisdiccion { Codigo = "MSN",  Nombre = "Misiones" },
        new Jurisdiccion { Codigo = "NQN",  Nombre = "Neuquén" },
        new Jurisdiccion { Codigo = "RN",   Nombre = "Río Negro" },
        new Jurisdiccion { Codigo = "SAL",  Nombre = "Salta" },
        new Jurisdiccion { Codigo = "SJ",   Nombre = "San Juan" },
        new Jurisdiccion { Codigo = "SL",   Nombre = "San Luis" },
        new Jurisdiccion { Codigo = "SC",   Nombre = "Santa Cruz" },
        new Jurisdiccion { Codigo = "SF",   Nombre = "Santa Fe" },
        new Jurisdiccion { Codigo = "SE",   Nombre = "Santiago del Estero" },
        new Jurisdiccion { Codigo = "TF",   Nombre = "Tierra del Fuego" },
        new Jurisdiccion { Codigo = "TUC",  Nombre = "Tucumán" },
    };
}
