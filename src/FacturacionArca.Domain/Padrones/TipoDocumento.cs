namespace FacturacionArca.Domain.Padrones;

public static class TipoDocumento
{
    public const int Cuit = 80;
    public const int Cuil = 86;
    public const int Cdi = 87;
    public const int LibretaEnrolamiento = 89;
    public const int LibretaCivica = 90;
    public const int Pasaporte = 94;
    public const int Dni = 96;
    public const int SinIdentificar = 99;

    public static bool EsValido(int codigo) => codigo is 80 or 86 or 87 or 89 or 90 or 94 or 96 or 99;
}
