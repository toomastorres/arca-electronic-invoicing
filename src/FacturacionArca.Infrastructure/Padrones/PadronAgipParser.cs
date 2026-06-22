using System.Globalization;
using FacturacionArca.Domain.Padrones;

namespace FacturacionArca.Infrastructure.Padrones;

/// <summary>
/// Parser streaming del archivo de padrón AGIP IIBB CABA.
/// Formato: <c>fechaPub;vigDesde;vigHasta;cuit;tipo;f1;f2;alPercep;alReten;regPercep;regReten;razon</c>
/// Cada fecha es DDMMYYYY (8 dígitos sin separador).
/// </summary>
public static class PadronAgipParser
{
    private static readonly CultureInfo CulInvariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Parsea el archivo línea a línea. No carga todo en memoria.
    /// </summary>
    public static IEnumerable<PadronIibbCaba> Parse(string rutaArchivo, CancellationToken ct = default)
    {
        if (!File.Exists(rutaArchivo))
            throw new FileNotFoundException("Archivo de padrón no encontrado", rutaArchivo);

        using var reader = new StreamReader(rutaArchivo);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var entry = ParseLine(line);
            if (entry is not null) yield return entry;
        }
    }

    public static PadronIibbCaba? ParseLine(string linea)
    {
        var parts = linea.Split(';');
        if (parts.Length < 11) return null;

        try
        {
            return new PadronIibbCaba
            {
                FechaPublicacion = ParseFechaCompacta(parts[0]),
                VigenciaDesde = ParseFechaCompacta(parts[1]),
                VigenciaHasta = ParseFechaCompacta(parts[2]),
                Cuit = parts[3].Trim(),
                TipoContribuyente = parts[4].Trim(),
                Flag1 = parts[5].Trim(),
                Flag2 = parts[6].Trim(),
                AlicuotaPercepcion = ParseDecimal(parts[7]),
                AlicuotaRetencion = ParseDecimal(parts[8]),
                RegimenPercepcion = parts[9].Trim(),
                RegimenRetencion = parts[10].Trim(),
                RazonSocial = parts.Length > 11 ? parts[11].Trim() : "",
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Convierte DDMMYYYY → DateOnly. Devuelve default si formato inválido.</summary>
    private static DateOnly ParseFechaCompacta(string s)
    {
        s = s.Trim();
        if (s.Length != 8) return default;
        if (!int.TryParse(s[..2], out var d)) return default;
        if (!int.TryParse(s[2..4], out var m)) return default;
        if (!int.TryParse(s[4..], out var y)) return default;
        try { return new DateOnly(y, m, d); }
        catch { return default; }
    }

    private static decimal ParseDecimal(string s) =>
        decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CulInvariant, out var v) ? v : 0m;
}
