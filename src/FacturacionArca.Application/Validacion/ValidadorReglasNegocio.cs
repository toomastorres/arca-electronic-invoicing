using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Padrones;

namespace FacturacionArca.Application.Validacion;

public sealed class ValidadorReglasNegocio
{
    public ResultadoValidacion Validar(Comprobante c)
    {
        var r = new ResultadoValidacion();

        if (c.Tipo.EsFacturaA())
        {
            if (c.Receptor.CodigoTipoDocumento != TipoDocumento.Cuit)
                r.AgregarError(nameof(c.Receptor.CodigoTipoDocumento),
                    "Para Factura/NC/ND A el receptor debe identificarse con CUIT (DocTipo=80).",
                    10063);

            if (c.Receptor.CondicionIva is not (CondicionIva.ResponsableInscripto or CondicionIva.Monotributo))
                r.AgregarError(nameof(c.Receptor.CondicionIva),
                    "Para Factura A el receptor debe ser Responsable Inscripto o Monotributista.",
                    10063);
        }

        if (c.Tipo.EsFacturaB())
        {
            if (c.Receptor.CondicionIva == CondicionIva.ResponsableInscripto)
                r.AgregarError(nameof(c.Receptor.CondicionIva),
                    "Una Factura B no se puede emitir a un Responsable Inscripto: corresponde Factura A.",
                    10064);
        }

        if (c.Concepto is Concepto.Servicios or Concepto.ProductosYServicios)
        {
            if (c.FechaServicioDesde is null || c.FechaServicioHasta is null || c.FechaVencimientoPago is null)
                r.AgregarError(nameof(c.Concepto),
                    "Para conceptos Servicios o Productos+Servicios son obligatorias las fechas de servicio (Desde/Hasta) y vencimiento de pago.",
                    10019);
        }

        if (string.IsNullOrWhiteSpace(c.Receptor.NumeroDocumento))
            r.AgregarError(nameof(c.Receptor.NumeroDocumento), "Falta el número de documento del receptor.");

        if (c.PuntoVenta <= 0)
            r.AgregarError(nameof(c.PuntoVenta), "El punto de venta no está configurado.");

        if (c.ImporteTotal <= 0 && !c.Tipo.EsNotaCredito())
            r.AgregarError(nameof(c.ImporteTotal), "El importe total del comprobante debe ser mayor a cero.");

        if (Moneda.PorCodigo(c.CodigoMoneda) is null)
            r.AgregarError(nameof(c.CodigoMoneda), $"Moneda '{c.CodigoMoneda}' no reconocida.");

        if (c.CodigoMoneda is not "PES" && c.CotizacionMoneda <= 0)
            r.AgregarError(nameof(c.CotizacionMoneda),
                "La cotización debe ser mayor a cero para monedas distintas de PES.",
                10020);

        // ── Validaciones FCE MiPyME ──
        if (c.Tipo.EsFce())
        {
            // Para Facturas FCE (201/206/211) el CBU y SCA/ADC son obligatorios
            bool esFacturaFce = c.Tipo is TipoComprobante.FacturaCreditoMipymeA
                or TipoComprobante.FacturaCreditoMipymeB
                or TipoComprobante.FacturaCreditoMipymeC;

            if (esFacturaFce)
            {
                if (c.EsSCA is null)
                    r.AgregarError(nameof(c.EsSCA),
                        "FCE: debe seleccionar el tipo de transmisión: SCA (Circulación Abierta) o ADC (Agente Depósito Colectivo).",
                        10162);

                if (string.IsNullOrWhiteSpace(c.CbuFce))
                    r.AgregarError(nameof(c.CbuFce),
                        "FCE: el CBU del beneficiario es obligatorio para Facturas de Crédito Electrónica.",
                        10162);
            }

            // Validación de formato CBU (si se proporcionó)
            if (!string.IsNullOrWhiteSpace(c.CbuFce))
            {
                var cbuLimpio = c.CbuFce.Trim();
                if (cbuLimpio.Length != 22 || !cbuLimpio.All(char.IsDigit))
                    r.AgregarError(nameof(c.CbuFce),
                        $"FCE: el CBU debe tener exactamente 22 dígitos numéricos (ingresado: {cbuLimpio.Length} caracteres).",
                        10163);
            }
        }

        return r;
    }
}
