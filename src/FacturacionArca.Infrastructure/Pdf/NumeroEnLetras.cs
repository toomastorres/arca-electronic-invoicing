namespace FacturacionArca.Infrastructure.Pdf;

/// <summary>
/// Convierte un monto decimal a su representación en letras en español (estilo AFIP/Argentina).
/// Ej: 720564.00 → "SETECIENTOS VEINTE MIL QUINIENTOS SESENTA Y CUATRO CON 00 CENTAVOS"
/// </summary>
public static class NumeroEnLetras
{
    private static readonly string[] Unidades =
    {
        "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS",
        "DIECISIETE", "DIECIOCHO", "DIECINUEVE"
    };

    private static readonly string[] Decenas =
    {
        "", "DIEZ", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA",
        "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    };

    private static readonly string[] Centenas =
    {
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    };

    public static string Convertir(decimal monto, string moneda = "Pesos")
    {
        var monedaUpper = moneda.ToUpperInvariant();
        if (monto == 0m) return $"CERO {monedaUpper} CON 00 CENTAVOS";

        var negativo = monto < 0;
        monto = Math.Abs(monto);

        var parteEntera = (long)Math.Truncate(monto);
        var parteCentavos = (int)Math.Round((monto - parteEntera) * 100);

        var letras = ConvertirEntero(parteEntera);

        // Singular/plural moneda
        var nombreMoneda = parteEntera == 1
            ? monedaUpper.TrimEnd('S')   // "PESOS" → "PESO", "DÓLARES" → "DÓLAR"
            : monedaUpper;

        var resultado = $"{letras} {nombreMoneda} CON {parteCentavos:D2} CENTAVOS";
        return negativo ? "MENOS " + resultado : resultado;
    }

    private static string ConvertirEntero(long numero)
    {
        if (numero == 0) return "CERO";
        if (numero < 0) return "MENOS " + ConvertirEntero(-numero);

        if (numero < 20) return Unidades[numero];
        if (numero < 100) return ConvertirDecenas(numero);
        if (numero < 1_000) return ConvertirCentenas(numero);
        if (numero < 1_000_000) return ConvertirMiles(numero);
        if (numero < 1_000_000_000) return ConvertirMillones(numero);
        if (numero < 1_000_000_000_000L) return ConvertirMilesMillones(numero);

        return numero.ToString();
    }

    private static string ConvertirDecenas(long n)
    {
        if (n < 20) return Unidades[n];
        if (n == 20) return "VEINTE";
        if (n < 30) return "VEINTI" + Unidades[n - 20].ToLower();

        var decena = Decenas[n / 10];
        var unidad = n % 10;
        return unidad == 0 ? decena : $"{decena} Y {Unidades[unidad]}";
    }

    private static string ConvertirCentenas(long n)
    {
        if (n == 100) return "CIEN";
        var centena = Centenas[n / 100];
        var resto = n % 100;
        return resto == 0 ? centena : $"{centena} {ConvertirDecenas(resto)}";
    }

    private static string ConvertirMiles(long n)
    {
        var miles = n / 1_000;
        var resto = n % 1_000;
        var prefijo = miles == 1 ? "MIL" : $"{ConvertirEntero(miles)} MIL";
        return resto == 0 ? prefijo : $"{prefijo} {ConvertirCentenas(resto)}";
    }

    private static string ConvertirMillones(long n)
    {
        var millones = n / 1_000_000;
        var resto = n % 1_000_000;
        var prefijo = millones == 1 ? "UN MILLÓN" : $"{ConvertirEntero(millones)} MILLONES";
        return resto == 0 ? prefijo : $"{prefijo} {ConvertirMiles(resto)}";
    }

    private static string ConvertirMilesMillones(long n)
    {
        var milesM = n / 1_000_000_000;
        var resto = n % 1_000_000_000;
        var prefijo = milesM == 1 ? "MIL MILLONES" : $"{ConvertirEntero(milesM)} MIL MILLONES";
        return resto == 0 ? prefijo : $"{prefijo} {ConvertirMillones(resto)}";
    }
}
