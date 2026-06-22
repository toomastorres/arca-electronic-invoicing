using FacturacionArca.Domain.Comprobantes;

namespace FacturacionArca.Application.Validacion;

public sealed class ValidadorAritmeticoAfip
{
    private const decimal ToleranciaAbsoluta = 0.01m;
    private const decimal ToleranciaRelativaPorcentual = 0.0001m;

    public ResultadoValidacion Validar(Comprobante c)
    {
        var r = new ResultadoValidacion();

        var esperadoTotal = c.ImporteNeto + c.ImporteNoGravado + c.ImporteExento + c.ImporteIva + c.ImporteTributos;
        if (!CoincideConTolerancia(c.ImporteTotal, esperadoTotal, c.ImporteTotal))
        {
            r.AgregarError(nameof(c.ImporteTotal),
                $"ImpTotal ({c.ImporteTotal:F2}) ≠ ImpTotConc + ImpNeto + ImpOpEx + ImpTrib + ImpIVA ({esperadoTotal:F2}).",
                10048);
        }

        if (c.SubtotalesIva.Count > 0)
        {
            var sumaBases = c.SubtotalesIva.Sum(s => s.BaseImponible);
            if (!CoincideConTolerancia(sumaBases, c.ImporteNeto, c.ImporteNeto))
            {
                r.AgregarError(nameof(c.SubtotalesIva),
                    $"Suma de bases imponibles de IVA ({sumaBases:F2}) ≠ ImpNeto ({c.ImporteNeto:F2}).",
                    10061);
            }

            var sumaIvas = c.SubtotalesIva.Sum(s => s.Importe);
            if (!CoincideConTolerancia(sumaIvas, c.ImporteIva, c.ImporteIva))
            {
                r.AgregarError(nameof(c.ImporteIva),
                    $"Suma de importes de IVA por alícuota ({sumaIvas:F2}) ≠ ImpIVA ({c.ImporteIva:F2}).",
                    10065);
            }
        }

        var sumaTributos = c.Tributos.Sum(t => t.Importe);
        if (!CoincideConTolerancia(sumaTributos, c.ImporteTributos, c.ImporteTributos))
        {
            r.AgregarError(nameof(c.ImporteTributos),
                $"Suma de tributos ({sumaTributos:F2}) ≠ ImpTrib ({c.ImporteTributos:F2}).",
                10100);
        }

        return r;
    }

    private static bool CoincideConTolerancia(decimal valor, decimal esperado, decimal referencia)
    {
        var diff = Math.Abs(valor - esperado);
        if (diff <= ToleranciaAbsoluta) return true;
        var refAbs = Math.Abs(referencia);
        if (refAbs == 0m) return false;
        return diff / refAbs <= ToleranciaRelativaPorcentual;
    }
}
