using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Domain.Padrones;
using FacturacionArca.Domain.Proformas;

namespace FacturacionArca.Application.UseCases;

public sealed class MapearProformaAComprobante
{
    public Comprobante Ejecutar(ProformaNapoles proforma, ConfiguracionEmpresa cfg, OpcionesMapeo opciones)
    {
        // Si hay un override de tipo (ej: reclasificar FC→FCE), usar ese; sino, el del XML
        TipoComprobante tipo;
        if (opciones.TipoComprobanteOverride is { } overrideTipo)
        {
            tipo = overrideTipo;
        }
        else
        {
            if (!Enum.IsDefined(typeof(TipoComprobante), proforma.CodigoTipoComprobanteOrigen))
                throw new InvalidOperationException(
                    $"CodigoTipoComprobante {proforma.CodigoTipoComprobanteOrigen} no se reconoce como tipo AFIP.");
            tipo = (TipoComprobante)proforma.CodigoTipoComprobanteOrigen;
        }

        var c = new Comprobante
        {
            ProformaOrigen = proforma.NumeroProforma,
            NumeroProforma = proforma.NumeroProforma,
            Tipo = tipo,
            Concepto = opciones.Concepto,
            CondicionVenta = ParseCondicionVenta(proforma.CondicionVentaTexto),
            PuntoVenta = opciones.PuntoVenta ?? cfg.PuntoVentaPorDefecto,
            FechaEmision = proforma.FechaEmision == default ? DateOnly.FromDateTime(DateTime.Today) : proforma.FechaEmision,
            FechaServicioDesde = opciones.FechaServicioDesde,
            FechaServicioHasta = opciones.FechaServicioHasta,
            FechaVencimientoPago = opciones.FechaVencimientoPago,
            CodigoMoneda = string.IsNullOrWhiteSpace(proforma.CodigoMoneda) ? "PES" : proforma.CodigoMoneda,
            CotizacionMoneda = proforma.CotizacionMoneda <= 0 ? 1m : proforma.CotizacionMoneda,
            ImporteNeto = proforma.ImporteGravado,
            ImporteNoGravado = proforma.ImporteNoGravado,
            ImporteExento = 0m,
            ImporteIva = proforma.ImporteIvaRi + proforma.ImporteIvaRni,
            ImporteTributos = 0m,
            ImporteTotal = proforma.ImporteTotal,
            BuqueViaje = proforma.BuqueViaje,
            PorCuenta = proforma.PorCuenta,
            Conocimiento = proforma.Conocimiento,
            CondicionIngresosBrutosSeleccionada = opciones.CondicionIngresosBrutos,
            Receptor = new ReceptorComprobante
            {
                CodigoTipoDocumento = proforma.CodigoTipoDocumento <= 0 ? TipoDocumento.Cuit : proforma.CodigoTipoDocumento,
                NumeroDocumento = proforma.NumeroDocumento,
                RazonSocial = proforma.Cliente,
                Domicilio = proforma.Domicilio,
                CondicionIva = opciones.CondicionIvaOverride ?? CondicionIvaParser.FromTextoNapoles(proforma.IvaCondicionTexto),
                CondicionIvaTextoOriginal = proforma.IvaCondicionTexto,
            },
        };

        // ── WARN-01: Normalizar importes negativos para NC/ND ──
        // ARCA espera valores positivos incluso para Notas de Crédito y Débito.
        // El ERP puede enviar valores negativos; los convertimos a positivos.
        if (tipo.EsNotaCredito() || tipo.EsNotaDebito())
        {
            c.ImporteNeto = Math.Abs(c.ImporteNeto);
            c.ImporteNoGravado = Math.Abs(c.ImporteNoGravado);
            c.ImporteIva = Math.Abs(c.ImporteIva);
            c.ImporteTotal = Math.Abs(c.ImporteTotal);
        }

        foreach (var i in proforma.Items)
        {
            c.Items.Add(new ItemComprobante
            {
                NumeroItem = i.NumeroItem,
                CodigoItem = i.CodigoItem,
                Descripcion = string.IsNullOrWhiteSpace(i.ItemDescripcion) ? i.DescripcionGrupo : i.ItemDescripcion,
                UnidadMedidaDescripcion = i.TarifaDescripcion,
                CodigoUnidadMedida = i.CodigoUnidadMedida == 0 ? 7 : i.CodigoUnidadMedida,
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario,
                CodigoCondicionIvaNapoles = i.CodigoCondicionIva,
                CodigoAlicuotaAfip = MapearCodigoAlicuota(i.CodigoCondicionIva, opciones.MapeoCondicionIvaPorDefecto),
                ImporteIva = i.ImporteIva,
                ImporteItem = i.ImporteItem,
                Referencias = i.Referencias,
                TarifaDescripcion = i.TarifaDescripcion,
                TipoCargoDescripcion = i.ImpoExpo,
            });
        }

        foreach (var s in proforma.SubtotalesIva)
        {
            var baseImponible = c.Items
                .Where(it => it.CodigoAlicuotaAfip == s.Codigo)
                .Sum(it => it.ImporteItem);

            c.SubtotalesIva.Add(new SubtotalIva
            {
                CodigoAlicuotaAfip = s.Codigo,
                BaseImponible = baseImponible,
                Importe = s.Importe,
            });
        }

        if (opciones.PercepcionIIBB is { } iibb && iibb.Importe > 0)
        {
            c.Tributos.Add(new Tributo
            {
                IdAfip = Tributo.IdIngresosBrutos,
                Descripcion = $"Percepción IIBB {iibb.Jurisdiccion}",
                BaseImponible = iibb.BaseImponible,
                Alicuota = iibb.Alicuota,
                Importe = iibb.Importe,
            });
            c.ImporteTributos += iibb.Importe;
            c.ImporteTotal += iibb.Importe;
        }

        if (opciones.ComprobanteAsociado is { } asoc && asoc.Numero > 0)
        {
            c.ComprobantesAsociados.Add(new ComprobanteAsociado
            {
                CodigoTipoComprobante = asoc.CodigoTipoComprobante,
                PuntoVenta = asoc.PuntoVenta,
                Numero = asoc.Numero,
                FechaEmision = asoc.FechaEmision,
                CuitEmisor = asoc.CuitEmisor,
            });
        }

        // Política interna de saldo (P/S) — sólo persistencia local
        c.TipoSaldo = opciones.TipoSaldo;
        c.SaldoMoneda = opciones.TipoSaldo == TipoSaldoCtaCte.Pesificado ? "PES" : c.CodigoMoneda;

        // FCE — Opcionales
        if (c.Tipo.EsFce())
        {
            c.EsSCA = opciones.FceEsSCA;
            c.CbuFce = opciones.FceCbu;
            c.AliasFce = opciones.FceAlias;
        }

        return c;
    }

    private static int MapearCodigoAlicuota(int codigoCondicionIvaNapoles, IDictionary<int, int>? mapeoOverride)
    {
        if (mapeoOverride is not null && mapeoOverride.TryGetValue(codigoCondicionIvaNapoles, out var afip))
            return afip;

        return codigoCondicionIvaNapoles switch
        {
            1 => 3,
            5 => 5,
            4 => 4,
            6 => 6,
            8 => 8,
            9 => 9,
            _ => 3,
        };
    }

    private static CondicionVenta ParseCondicionVenta(string texto) =>
        (texto ?? "").Trim().ToUpperInvariant() switch
        {
            "CONTADO" or "CONT" => CondicionVenta.Contado,
            "CUENTA CORRIENTE" or "CUENTA" or "CC" => CondicionVenta.Cuenta,
            _ => CondicionVenta.OtraForma,
        };
}

public sealed class OpcionesMapeo
{
    public int? PuntoVenta { get; set; }
    public Concepto Concepto { get; set; } = Concepto.Servicios;
    public DateOnly? FechaServicioDesde { get; set; }
    public DateOnly? FechaServicioHasta { get; set; }
    public DateOnly? FechaVencimientoPago { get; set; }
    public string CondicionIngresosBrutos { get; set; } = "Convenio Multilateral";
    public PercepcionIIBB? PercepcionIIBB { get; set; }
    public IDictionary<int, int>? MapeoCondicionIvaPorDefecto { get; set; }
    public CondicionIva? CondicionIvaOverride { get; set; }

    /// <summary>
    /// Si tiene valor, sobreescribe el tipo de comprobante que viene del XML de la proforma.
    /// Permite reclasificar una FC normal (ej: tipo 1) como FCE MiPyME (ej: tipo 201).
    /// </summary>
    public TipoComprobante? TipoComprobanteOverride { get; set; }

    /// <summary>
    /// Comprobante asociado (NC/ND → factura origen). Si hay datos en la proforma XML,
    /// la app los pre-poblará; el usuario puede editarlos en el panel de detalle.
    /// </summary>
    public ComprobanteAsociadoMapeo? ComprobanteAsociado { get; set; }

    /// <summary>Tipo de saldo cta cte (P=pesificado, S=en moneda original).</summary>
    public TipoSaldoCtaCte TipoSaldo { get; set; } = TipoSaldoCtaCte.Pesificado;

    /// <summary>Solo aplica a FCE: true=SCA (Opcional 27="S"), false=ADC (Opcional 27="N").</summary>
    public bool? FceEsSCA { get; set; }

    /// <summary>FCE — CBU del emisor (Opcional 2101).</summary>
    public string? FceCbu { get; set; }

    /// <summary>FCE — Alias bancario del emisor (Opcional 2102).</summary>
    public string? FceAlias { get; set; }
}

public sealed class ComprobanteAsociadoMapeo
{
    public int CodigoTipoComprobante { get; set; }
    public int PuntoVenta { get; set; }
    public long Numero { get; set; }
    public DateOnly? FechaEmision { get; set; }
    public string? CuitEmisor { get; set; }
}

public sealed record PercepcionIIBB(string Jurisdiccion, decimal BaseImponible, decimal Alicuota, decimal Importe);
