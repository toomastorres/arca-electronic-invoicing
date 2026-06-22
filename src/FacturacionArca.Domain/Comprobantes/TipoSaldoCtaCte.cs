namespace FacturacionArca.Domain.Comprobantes;

/// <summary>
/// Política interna de tratamiento del saldo en cuenta corriente para facturas en moneda extranjera.
/// ARCA no diferencia (recibe siempre MonId + MonCotiz); este flag rige solo la persistencia local
/// y el módulo de cobranzas.
/// </summary>
public enum TipoSaldoCtaCte
{
    /// <summary>"P" — El saldo se fija en pesos al obtener el CAE (USD × cotización). No se ajusta luego.</summary>
    Pesificado = 0,

    /// <summary>"S" — El saldo permanece en moneda original. Se calcula diferencia de cambio en cada pago.</summary>
    EnMonedaOriginal = 1,
}

public static class TipoSaldoCtaCteExtensions
{
    public static string Codigo(this TipoSaldoCtaCte t) => t == TipoSaldoCtaCte.Pesificado ? "P" : "S";
    public static string Descripcion(this TipoSaldoCtaCte t) =>
        t == TipoSaldoCtaCte.Pesificado ? "Pesificada (saldo fijo en ARS)" : "Saldos (saldo en moneda original)";
}
