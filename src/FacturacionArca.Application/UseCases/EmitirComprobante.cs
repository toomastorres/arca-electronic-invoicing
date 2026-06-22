using FacturacionArca.Application.Abstractions;
using FacturacionArca.Application.Validacion;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Domain.Errores;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Application.UseCases;

public sealed class EmitirComprobante
{
    private readonly IArcaWsfev1Client _wsfe;
    private readonly IArcaWsfecredClient _wsfecred;
    private readonly IComprobanteRepository _repo;
    private readonly IArchivoFiscalRepository _archivo;
    private readonly ValidadorAritmeticoAfip _validadorAritmetico;
    private readonly ValidadorReglasNegocio _validadorReglas;
    private readonly ResolverContingenciaRed _contingencia;
    private readonly IConfiguracionRepository _configRepo;
    private readonly EscribirCallbackOut _callback;
    private readonly INotificacionService _notificaciones;
    private readonly ILogger<EmitirComprobante> _logger;

    public EmitirComprobante(
        IArcaWsfev1Client wsfe,
        IArcaWsfecredClient wsfecred,
        IComprobanteRepository repo,
        IArchivoFiscalRepository archivo,
        ValidadorAritmeticoAfip validadorAritmetico,
        ValidadorReglasNegocio validadorReglas,
        ResolverContingenciaRed contingencia,
        IConfiguracionRepository configRepo,
        EscribirCallbackOut callback,
        INotificacionService notificaciones,
        ILogger<EmitirComprobante> logger)
    {
        _wsfe = wsfe;
        _wsfecred = wsfecred;
        _repo = repo;
        _archivo = archivo;
        _validadorAritmetico = validadorAritmetico;
        _validadorReglas = validadorReglas;
        _contingencia = contingencia;
        _configRepo = configRepo;
        _callback = callback;
        _notificaciones = notificaciones;
        _logger = logger;
    }

    public async Task<ResultadoEmision> EjecutarAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        // ── Validación local ──
        var validacion = new ResultadoValidacion();
        validacion.Combinar(_validadorReglas.Validar(comprobante));
        validacion.Combinar(_validadorAritmetico.Validar(comprobante));
        if (!validacion.EsValido)
        {
            _logger.LogWarning("Validación local del comprobante falló: {Resumen}", validacion.ResumenAmigable);

            // Notificar cada error de validación al usuario
            foreach (var error in validacion.Errores)
            {
                var codigoStr = error.CodigoArca?.ToString();
                var accion = ObtenerAccionSugerida(error);
                _notificaciones.Error(
                    "Validación fallida",
                    error.Mensaje,
                    codigoStr,
                    accion);
            }

            return ResultadoEmision.RechazadoLocal(validacion);
        }

        var cfg = await _configRepo.GetAsync(ct);

        // ── Advertencias pre-emisión (no bloquean, pero informan) ──
        EmitirAdvertenciasPreEmision(comprobante, cfg);

        if (comprobante.Numero is null)
        {
            var ultimo = await _wsfe.ObtenerUltimoAutorizadoAsync(comprobante.PuntoVenta, comprobante.Tipo, ct);
            comprobante.Numero = ultimo + 1;
            _logger.LogInformation("Asignado número {Numero} (último autorizado: {Ultimo}) para Pto {PtoVta} Tipo {Tipo}.",
                comprobante.Numero, ultimo, comprobante.PuntoVenta, comprobante.Tipo);
        }

        try
        {
            // ── Pesificación en Vuelo ──
            // ARCA rechaza facturas en moneda extranjera si la cotización difiere de la oficial.
            // Para evitar esto con cotizaciones de agencia, pesificamos el XML enviado a ARCA.
            var cFiscal = GenerarClonPesificado(comprobante);

            var respuesta = comprobante.Tipo.EsFce()
                ? await _wsfecred.EmitirFceAsync(cFiscal, ct)
                : await _wsfe.SolicitarCaeAsync(cFiscal, ct);

            return await ProcesarRespuestaAsync(comprobante, respuesta, cfg, ct);
        }
        catch (Exception ex) when (EsErrorDeRed(ex))
        {
            _logger.LogWarning(ex, "Error de red al solicitar CAE. Iniciando contingencia.");
            _notificaciones.Advertencia(
                "Error de red",
                "No se pudo conectar con ARCA. Verificando si el comprobante fue procesado...",
                accionSugerida: "Verifique la conexión a Internet. El sistema intentará recuperar automáticamente.");

            var resuelto = await _contingencia.EjecutarAsync(comprobante, ct);
            if (resuelto is not null)
            {
                _notificaciones.Exito(
                    "Contingencia resuelta",
                    $"ARCA ya había procesado el comprobante. CAE recuperado: {resuelto.Cae}");
                return await ProcesarRespuestaAsync(comprobante, resuelto, cfg, ct);
            }

            comprobante.Observaciones = $"Pendiente de reproceso: {ex.Message}";
            await _repo.AddAsync(comprobante, ct);

            _notificaciones.Error(
                "Comprobante pendiente",
                $"ARCA no procesó el comprobante. Queda pendiente de reproceso.",
                accionSugerida: "Reintente cuando la conexión se restablezca. Verifique el estado en el Historial.");

            return ResultadoEmision.PendienteReproceso(ex.Message);
        }
        catch (ArcaException arcaEx)
        {
            _logger.LogError(arcaEx, "ARCA rechazó la emisión del comprobante.");

            // Notificar cada error ARCA al usuario con acción sugerida
            foreach (var error in arcaEx.Errores)
            {
                var accionArca = ObtenerAccionSugeridaArca(error.Codigo);
                _notificaciones.Error(
                    "Rechazo de ARCA",
                    error.MensajeAmigable,
                    error.Codigo.ToString(),
                    accionArca);
            }

            return ResultadoEmision.RechazadoArca(arcaEx);
        }
    }

    private async Task<ResultadoEmision> ProcesarRespuestaAsync(
        Comprobante c, RespuestaCae r, ConfiguracionEmpresa cfg, CancellationToken ct)
    {
        c.Numero = r.NumeroComprobante;
        c.Cae = new Cae(r.Cae, r.FechaVencimiento, r.FechaProceso);
        c.RespuestaArcaXml = r.XmlRespuesta;
        c.FechaEmisionEfectiva = DateTime.UtcNow;

        // ── Política saldo cta cte (P/S) — flag para cliente y export al ERP ──
        var totalArs = c.ImporteTotal * c.CotizacionMoneda;
        c.SaldoArsFijado = totalArs; // siempre se calcula y persiste como referencia
        c.SaldoMoneda = c.TipoSaldo == TipoSaldoCtaCte.Pesificado ? "PES" : c.CodigoMoneda;

        if (!string.IsNullOrWhiteSpace(cfg.CarpetaArchivoFiscal))
            await _archivo.GuardarRespuestaAsync(c, r.XmlRespuesta, cfg.CarpetaArchivoFiscal, ct);

        if (c.Id == 0) await _repo.AddAsync(c, ct);
        else await _repo.UpdateAsync(c, ct);

        // Escribir callback XML al ERP Nápoles (carpeta OUT)
        if (!string.IsNullOrWhiteSpace(cfg.CarpetaProformasProcesadas))
        {
            try { await _callback.EjecutarAsync(c, cfg.CarpetaProformasProcesadas, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo escribir callback OUT XML.");
                _notificaciones.Advertencia(
                    "Callback OUT fallido",
                    $"No se pudo escribir el XML de confirmación al ERP: {ex.Message}",
                    accionSugerida: "Verifique permisos y espacio en la carpeta de salida.");
            }
        }

        _logger.LogInformation("Comprobante {Tipo} {PtoVta:0000}-{Nro:00000000} autorizado con CAE {Cae} (vto {Vto:yyyy-MM-dd}).",
            c.Tipo, c.PuntoVenta, c.Numero, r.Cae, r.FechaVencimiento);

        _notificaciones.Exito(
            "Comprobante autorizado",
            $"{c.Tipo.LetraVisible()} {c.PuntoVenta:0000}-{c.Numero:00000000} — CAE: {r.Cae} — Vto: {r.FechaVencimiento:dd/MM/yyyy}");

        return ResultadoEmision.Aprobado(c);
    }

    /// <summary>
    /// Genera una copia del comprobante convertida a PESOS según la cotización pactada.
    /// Esto asegura que ARCA apruebe la factura sin validar contra la cotización del Banco Nación,
    /// mientras que el dominio local mantiene la moneda original (DOL/EUR) para la impresión y el ERP.
    /// </summary>
    private static Comprobante GenerarClonPesificado(Comprobante c)
    {
        if (c.CodigoMoneda == "PES") return c;

        var clone = new Comprobante
        {
            Id = c.Id,
            NumeroProforma = c.NumeroProforma,
            Tipo = c.Tipo,
            Concepto = c.Concepto,
            CondicionVenta = c.CondicionVenta,
            PuntoVenta = c.PuntoVenta,
            Numero = c.Numero,
            FechaEmision = c.FechaEmision,
            FechaServicioDesde = c.FechaServicioDesde,
            FechaServicioHasta = c.FechaServicioHasta,
            FechaVencimientoPago = c.FechaVencimientoPago,
            Receptor = c.Receptor,
            
            // Fiscally PES
            CodigoMoneda = "PES",
            CotizacionMoneda = 1m,
            
            // Amounts multiplied by exchange rate
            ImporteNeto = Math.Round(c.ImporteNeto * c.CotizacionMoneda, 2),
            ImporteNoGravado = Math.Round(c.ImporteNoGravado * c.CotizacionMoneda, 2),
            ImporteExento = Math.Round(c.ImporteExento * c.CotizacionMoneda, 2),
            ImporteIva = Math.Round(c.ImporteIva * c.CotizacionMoneda, 2),
            ImporteTributos = Math.Round(c.ImporteTributos * c.CotizacionMoneda, 2),
            ImporteTotal = Math.Round(c.ImporteTotal * c.CotizacionMoneda, 2),
            
            EsSCA = c.EsSCA,
            CbuFce = c.CbuFce,
            AliasFce = c.AliasFce,
        };

        foreach (var iva in c.SubtotalesIva)
        {
            clone.SubtotalesIva.Add(new SubtotalIva
            {
                CodigoAlicuotaAfip = iva.CodigoAlicuotaAfip,
                BaseImponible = Math.Round(iva.BaseImponible * c.CotizacionMoneda, 2),
                Importe = Math.Round(iva.Importe * c.CotizacionMoneda, 2)
            });
        }

        foreach (var tri in c.Tributos)
        {
            clone.Tributos.Add(new Tributo
            {
                IdAfip = tri.IdAfip,
                Descripcion = tri.Descripcion,
                BaseImponible = Math.Round(tri.BaseImponible * c.CotizacionMoneda, 2),
                Alicuota = tri.Alicuota,
                Importe = Math.Round(tri.Importe * c.CotizacionMoneda, 2)
            });
        }

        foreach (var asoc in c.ComprobantesAsociados) clone.ComprobantesAsociados.Add(asoc);

        return clone;
    }

    /// <summary>
    /// Emite advertencias que no bloquean la emisión pero informan al usuario.
    /// </summary>
    private void EmitirAdvertenciasPreEmision(Comprobante c, ConfiguracionEmpresa cfg)
    {
        // WARN-02: Monotributo recibiendo Factura A
        if (c.Tipo.EsFacturaA() && c.Receptor.CondicionIva == CondicionIva.Monotributo)
        {
            _notificaciones.Advertencia(
                "Receptor Monotributista con Factura A",
                $"El receptor {c.Receptor.RazonSocial} es Monotributista. Normalmente le corresponde Factura B.",
                "10063",
                "Verifique la condición IVA del receptor. Si es correcto, continúe.");
        }

        // CRIT-02: FCE - verificar monto mínimo
        if (c.Tipo.EsFce())
        {
            // Monto mínimo FCE vigente (configurable)
            var totalArs = c.ImporteTotal * c.CotizacionMoneda;
            if (totalArs < cfg.MontoMinimoFce)
            {
                _notificaciones.Info(
                    "FCE por debajo del monto mínimo",
                    $"El total ARS ${totalArs:N2} es inferior al monto mínimo FCE configurado (${cfg.MontoMinimoFce:N0}). " +
                    "La emisión como FCE es posible pero no obligatoria para este importe.");
            }
        }

        // Advertencia si es factura normal pero el total supera el monto mínimo FCE
        if (!c.Tipo.EsFce() && c.Tipo.EquivalenteFce() is not null)
        {
            var totalArs = c.ImporteTotal * c.CotizacionMoneda;
            if (totalArs >= cfg.MontoMinimoFce)
            {
                _notificaciones.Advertencia(
                    "Posible obligación FCE",
                    $"El total ARS ${totalArs:N2} supera el monto mínimo FCE configurado (${cfg.MontoMinimoFce:N0}). " +
                    "Si el receptor es Empresa Grande, esta operación debería emitirse como FCE.",
                    accionSugerida: "Considere activar 'Forzar como FCE' si el receptor está en el listado de Empresas Grandes de ARCA.");
            }
        }
    }

    /// <summary>Sugiere acción para errores de validación local.</summary>
    private static string? ObtenerAccionSugerida(ErrorValidacion error)
    {
        if (error.CodigoArca is null) return null;
        return error.CodigoArca switch
        {
            10019 => "Complete las fechas de Servicio Desde, Hasta y Vencimiento de Pago.",
            10020 => "Verifique la cotización de moneda con la oficial de ARCA.",
            10063 => "Verifique la condición IVA y tipo de documento del receptor.",
            10064 => "Cambie el tipo de comprobante a Factura A para Responsable Inscripto.",
            10162 => "Complete el CBU y seleccione SCA o ADC en el panel FCE.",
            10163 => "El CBU debe tener exactamente 22 dígitos numéricos.",
            _ => null,
        };
    }

    /// <summary>Sugiere acción para errores devueltos por ARCA.</summary>
    private static string? ObtenerAccionSugeridaArca(int codigo) => codigo switch
    {
        600 or 600100 => "Espere unos minutos y reintente. Verifique el estado de servicios ARCA.",
        1005 => "El sistema renovará el ticket automáticamente. Reintente.",
        1101 or 10013 => "Verifique que el punto de venta esté autorizado en ARCA con tipo RECE.",
        10015 or 10017 => "Verifique la fecha de emisión. ARCA permite ±5 días (productos) o ±10 días (servicios).",
        10016 => "Cierre la aplicación, espere 30 segundos y reintente. No ejecute 2 instancias.",
        10018 => "Verifique que el tipo de documento coincida con el tipo de comprobante (CUIT para A).",
        10019 => "Complete Servicio Desde, Hasta y Vencimiento de Pago para comprobantes de servicios.",
        10020 or 10096 => "Verifique la cotización de moneda con el tipo de cambio oficial de ARCA.",
        10048 => "Revise los montos en el ERP: Total debe ser = Neto + NoGrav + Exento + IVA + Tributos.",
        10061 => "Revise la agrupación de IVA: la suma de bases imponibles debe igualar el importe neto.",
        10063 => "El receptor de Factura A debe ser RI con CUIT. Verifique datos.",
        10064 => "Factura B no se puede emitir a RI. Cambie a Factura A.",
        10065 => "Revise los importes de IVA por alícuota. La suma debe coincidir con el total IVA.",
        10070 or 10071 => "Verifique el CUIT del receptor en el padrón de AFIP/ARCA.",
        10076 => "Las fechas de servicio están fuera del rango permitido. Ajuste las fechas.",
        10100 => "Revise todos los subtotales del comprobante. Hay errores aritméticos.",
        10154 => "El comprobante ya existe en ARCA. Consulte el Historial para ver si ya fue emitido.",
        10162 => "Complete el CBU y SCA/ADC en el panel FCE antes de emitir.",
        10163 => "El CBU informado no es válido. Verifique que tenga 22 dígitos numéricos.",
        10164 => "El receptor no está inscripto en el Registro FCE MiPyME. Consulte con el receptor.",
        10165 => "Debe constituir el Domicilio Fiscal Electrónico en ARCA antes de emitir FCE.",
        10166 => "Verifique que su empresa tenga categorización MiPyME vigente en ARCA.",
        10180 => "El comprobante asociado no existe en ARCA. Verifique tipo, punto de venta y número.",
        602 => "El comprobante consultado no existe. Verifique los datos del comprobante asociado.",
        _ => null,
    };

    private static bool EsErrorDeRed(Exception ex) =>
        ex is System.Net.Sockets.SocketException
           or TaskCanceledException
           or TimeoutException
           or HttpRequestException
        || ex.GetType().FullName == "System.ServiceModel.CommunicationException";
}

public sealed class ResultadoEmision
{
    public bool Aprobada { get; private init; }
    public Comprobante? Comprobante { get; private init; }
    public ResultadoValidacion? ErroresLocales { get; private init; }
    public ArcaException? ErroresArca { get; private init; }
    public string? MotivoPendiente { get; private init; }

    public static ResultadoEmision Aprobado(Comprobante c) => new() { Aprobada = true, Comprobante = c };
    public static ResultadoEmision RechazadoLocal(ResultadoValidacion v) => new() { Aprobada = false, ErroresLocales = v };
    public static ResultadoEmision RechazadoArca(ArcaException e) => new() { Aprobada = false, ErroresArca = e };
    public static ResultadoEmision PendienteReproceso(string motivo) => new() { Aprobada = false, MotivoPendiente = motivo };
}
