using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Application.UseCases;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Domain.Proformas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Wpf.ViewModels;

public partial class DetalleProformaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DetalleProformaViewModel> _logger;
    private readonly INotificacionService _notificaciones;

    [ObservableProperty] private ProformaNapoles? proforma;
    [ObservableProperty] private string condicionIngresosBrutos = "Convenio Multilateral";
    [ObservableProperty] private Concepto concepto = Concepto.Servicios;
    [ObservableProperty] private DateTime fechaServicioDesde = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime fechaServicioHasta = DateTime.Today;
    [ObservableProperty] private DateTime fechaVencimientoPago = DateTime.Today.AddDays(15);
    [ObservableProperty] private string? mensajeEstado;
    [ObservableProperty] private bool emitiendo;
    [ObservableProperty] private Comprobante? ultimoEmitido;

    // ── Percepción IIBB CABA (auto desde padrón) ──
    [ObservableProperty] private bool aplicarPercepcionCaba = true;
    [ObservableProperty] private decimal alicuotaPercepcionCaba;
    [ObservableProperty] private decimal importePercepcionCaba;
    [ObservableProperty] private string origenPercepcion = "—";
    [ObservableProperty] private bool percepcionManual;

    // ── Comprobante asociado (NC↔Factura origen) ──
    public ObservableCollection<ComprobanteAsociadoOption> AsociadosDisponibles { get; } = new();
    [ObservableProperty] private ComprobanteAsociadoOption? asociadoSeleccionado;
    [ObservableProperty] private bool requiereAsociado;
    [ObservableProperty] private bool asociadoEditable;
    [ObservableProperty] private int asociadoTipo = 1;
    [ObservableProperty] private int asociadoPuntoVenta;
    [ObservableProperty] private long asociadoNumero;
    [ObservableProperty] private DateTime? asociadoFecha;

    // ── Tipo de saldo (P / S) — solo aplica a moneda extranjera ──
    [ObservableProperty] private TipoSaldoCtaCte tipoSaldo = TipoSaldoCtaCte.Pesificado;
    [ObservableProperty] private bool requiereTipoSaldo;
    public IReadOnlyList<TipoSaldoCtaCte> OpcionesTipoSaldo { get; } = new[]
    {
        TipoSaldoCtaCte.Pesificado,
        TipoSaldoCtaCte.EnMonedaOriginal,
    };

    // ── FCE — SCA / ADC ──
    [ObservableProperty] private bool esFce;
    [ObservableProperty] private bool fceEsSCA = true;          // true=SCA, false=ADC
    [ObservableProperty] private string fceCbu = "";
    [ObservableProperty] private string fceAlias = "";

    // ── Forzar FC normal como FCE (override tipo comprobante) ──
    /// <summary>true si el comprobante XML es tipo normal (1/6/etc.) y se puede convertir a FCE (201/206/etc.).</summary>
    [ObservableProperty] private bool puedeForzarFce;
    /// <summary>Cuando el usuario marca esta opción, la FC se emite como FCE con los Opcionales correspondientes.</summary>
    [ObservableProperty] private bool forzarComoFce;
    [ObservableProperty] private bool mostrarAvisoFce;

    // ── Condición IVA (Override) ──
    [ObservableProperty] private CondicionIva condicionIvaOverride;
    public IReadOnlyList<CondicionIva> OpcionesCondicionIva { get; } = Enum.GetValues<CondicionIva>();

    public IReadOnlyList<string> OpcionesCondicionIIBB { get; } = new[]
    {
        "Convenio Multilateral",
        "Local / No Inscripto",
        "Exento",
    };

    public IReadOnlyList<Concepto> OpcionesConcepto { get; } = new[]
    {
        Concepto.Productos, Concepto.Servicios, Concepto.ProductosYServicios,
    };

    public DetalleProformaViewModel(IServiceScopeFactory scopeFactory, ILogger<DetalleProformaViewModel> logger, INotificacionService notificaciones)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _notificaciones = notificaciones;
    }

    public async void Cargar(ProformaNapoles p)
    {
        Proforma = p;
        var tipo = (TipoComprobante)p.CodigoTipoComprobanteOrigen;
        RequiereAsociado = tipo.RequiereAsociado();
        EsFce = tipo.EsFce();

        // Condición IVA Override (default a lo que detectó de la proforma)
        CondicionIvaOverride = CondicionIvaParser.FromTextoNapoles(p.IvaCondicionTexto);

        using var scope = _scopeFactory.CreateScope();
        var configRepo = scope.ServiceProvider.GetRequiredService<IConfiguracionRepository>();
        var cfg = await configRepo.GetAsync();

        // Permite forzar como FCE si el tipo XML es normal (1/6/3/etc.) y tiene equivalente FCE
        PuedeForzarFce = !tipo.EsFce() && tipo.EquivalenteFce() is not null;
        ForzarComoFce = false;
        MostrarAvisoFce = PuedeForzarFce && p.ImporteTotal >= cfg.MontoMinimoFce;

        // Tipo P/S sólo tiene sentido si la moneda no es PES
        RequiereTipoSaldo = !string.IsNullOrWhiteSpace(p.CodigoMoneda)
            && !p.CodigoMoneda.Equals("PES", StringComparison.OrdinalIgnoreCase);
        // Default: pesificado (más conservador) si moneda extranjera
        TipoSaldo = TipoSaldoCtaCte.Pesificado;

        await CargarAsociadosDisponiblesAsync(p);
        await RecalcularPercepcionAsync();
    }

    partial void OnForzarComoFceChanged(bool value)
    {
        // Al forzar como FCE, activamos el panel FCE; al desactivar, volvemos al tipo original
        if (Proforma is null) return;
        var tipoOriginal = (TipoComprobante)Proforma.CodigoTipoComprobanteOrigen;
        EsFce = value ? true : tipoOriginal.EsFce();
        RequiereAsociado = value
            ? (tipoOriginal.EquivalenteFce()?.RequiereAsociado() ?? tipoOriginal.RequiereAsociado())
            : tipoOriginal.RequiereAsociado();
    }

    // ─────────────────────────────────────────────
    // Cambios reactivos
    // ─────────────────────────────────────────────
    partial void OnCondicionIngresosBrutosChanged(string value) =>
        _ = RecalcularPercepcionAsync();

    partial void OnAplicarPercepcionCabaChanged(bool value)
    {
        if (!value)
        {
            ImportePercepcionCaba = 0;
            AlicuotaPercepcionCaba = 0;
            OrigenPercepcion = "Desactivado";
        }
        else
        {
            _ = RecalcularPercepcionAsync();
        }
    }

    partial void OnAsociadoSeleccionadoChanged(ComprobanteAsociadoOption? value)
    {
        if (value is null) { AsociadoEditable = false; return; }

        if (ReferenceEquals(value, ComprobanteAsociadoOption.Manual))
        {
            AsociadoEditable = true;
            return;
        }

        AsociadoEditable = false;
        AsociadoTipo = value.CodigoTipoComprobante;
        AsociadoPuntoVenta = value.PuntoVenta;
        AsociadoNumero = value.Numero;
        AsociadoFecha = value.FechaEmision?.ToDateTime(TimeOnly.MinValue);
    }

    // ─────────────────────────────────────────────
    // Cálculo automático de percepción IIBB CABA
    // ─────────────────────────────────────────────
    private async Task RecalcularPercepcionAsync()
    {
        if (Proforma is null) return;

        // Si no es Convenio Multilateral, no aplica percepción CABA
        if (!CondicionIngresosBrutos.StartsWith("Convenio", StringComparison.OrdinalIgnoreCase) || !AplicarPercepcionCaba)
        {
            ImportePercepcionCaba = 0;
            AlicuotaPercepcionCaba = 0;
            OrigenPercepcion = AplicarPercepcionCaba ? "No aplica (no es CM)" : "Desactivado";
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var calc = sp.GetRequiredService<CalcularPercepcionIibbCaba>();
            // Convertir base imponible a ARS (lo que se grava IIBB)
            var baseArs = Proforma.ImporteGravado * (Proforma.CotizacionMoneda <= 0 ? 1m : Proforma.CotizacionMoneda);

            var r = await calc.EjecutarAsync(Proforma.NumeroDocumento, baseArs, Proforma.FechaEmision);
            if (r.Aplica)
            {
                AlicuotaPercepcionCaba = r.Alicuota;
                ImportePercepcionCaba = r.Importe;
                OrigenPercepcion = $"Padrón AGIP (régimen {r.RegimenAgip})";
            }
            else
            {
                AlicuotaPercepcionCaba = 0;
                ImportePercepcionCaba = 0;
                OrigenPercepcion = "Sin entrada en padrón";
            }
            PercepcionManual = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculando percepción CABA");
            OrigenPercepcion = "Error al consultar padrón";
        }
    }

    // ─────────────────────────────────────────────
    // Carga lista de comprobantes asociables
    // ─────────────────────────────────────────────
    private async Task CargarAsociadosDisponiblesAsync(ProformaNapoles p)
    {
        AsociadosDisponibles.Clear();

        // 1) Si el XML proforma trae asociado pre-cargado, lo proponemos
        if (p.ComprobanteAsociado is { CodigoTipoComprobante: > 0, NumeroComprobante: > 0 } a)
        {
            var opt = ComprobanteAsociadoOption.DesdeProforma(
                a.CodigoTipoComprobante!.Value,
                a.NumeroPuntoVenta ?? 0,
                a.NumeroComprobante!.Value,
                a.FechaEmision,
                a.CuitEmisor);
            AsociadosDisponibles.Add(opt);
        }

        // 2) Cargar comprobantes históricos del mismo receptor (mismo CUIT)
        if (!string.IsNullOrWhiteSpace(p.NumeroDocumento))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IComprobanteRepository>();

                var tipoActual = (TipoComprobante)p.CodigoTipoComprobanteOrigen;
                var tipoOrigen = tipoActual.TipoFacturaOrigen();
                // Buscar facturas de origen del mismo receptor
                var historicos = await repo.ListarPorReceptorAsync(p.NumeroDocumento, tipoOrigen, max: 50);
                foreach (var c in historicos)
                    AsociadosDisponibles.Add(ComprobanteAsociadoOption.DesdeComprobante(c));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error listando comprobantes asociables");
            }
        }

        // 3) Opción manual al final
        AsociadosDisponibles.Add(ComprobanteAsociadoOption.Manual);

        // Auto-seleccionar la primera (la del XML si existe, sino el más reciente)
        AsociadoSeleccionado = AsociadosDisponibles.Count > 1 ? AsociadosDisponibles[0] : null;
    }

    // ─────────────────────────────────────────────
    // Mover XML procesado a OUT
    // ─────────────────────────────────────────────
    private void MoverProformaProcesada(ProformaNapoles p, ConfiguracionEmpresa cfg)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(p.ArchivoOrigen) || !File.Exists(p.ArchivoOrigen))
                return;
            if (string.IsNullOrWhiteSpace(cfg.CarpetaProformasProcesadas))
                return;

            Directory.CreateDirectory(cfg.CarpetaProformasProcesadas);
            var nombre = Path.GetFileName(p.ArchivoOrigen);
            var destino = Path.Combine(cfg.CarpetaProformasProcesadas, nombre);
            if (File.Exists(destino))
            {
                var sufijo = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var sinExt = Path.GetFileNameWithoutExtension(nombre);
                var ext = Path.GetExtension(nombre);
                destino = Path.Combine(cfg.CarpetaProformasProcesadas, $"{sinExt}_{sufijo}{ext}");
            }
            File.Move(p.ArchivoOrigen, destino);
            _logger.LogInformation("Proforma procesada movida a {Destino}.", destino);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo mover la proforma procesada {Archivo}.", p.ArchivoOrigen);
        }
    }

    // ─────────────────────────────────────────────
    // Emitir Factura Electrónica
    // ─────────────────────────────────────────────
    [RelayCommand]
    public async Task EnviarFacturaElectronicaAsync()
    {
        if (Proforma is null) return;

        // Validar asociado si lo requiere
        if (RequiereAsociado && !ValidarAsociado(out var motivo))
        {
            MensajeEstado = motivo;
            return;
        }

        try
        {
            Emitiendo = true;
            MensajeEstado = "Validando y enviando a ARCA...";

            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var configRepo = sp.GetRequiredService<IConfiguracionRepository>();
            var mapeador = sp.GetRequiredService<MapearProformaAComprobante>();
            var emisor = sp.GetRequiredService<EmitirComprobante>();
            var pdf = sp.GetRequiredService<GenerarPdfComprobante>();
            var imprimir = sp.GetRequiredService<ImprimirComprobante>();
            var proformaRepo = sp.GetRequiredService<IProformaRepository>();

            var cfg = await configRepo.GetAsync();

            var opciones = new OpcionesMapeo
            {
                Concepto = Concepto,
                FechaServicioDesde = Concepto != Concepto.Productos ? DateOnly.FromDateTime(FechaServicioDesde) : null,
                FechaServicioHasta = Concepto != Concepto.Productos ? DateOnly.FromDateTime(FechaServicioHasta) : null,
                FechaVencimientoPago = (Concepto != Concepto.Productos || EsFce || ForzarComoFce) ? DateOnly.FromDateTime(FechaVencimientoPago) : null,
                CondicionIngresosBrutos = CondicionIngresosBrutos,
                CondicionIvaOverride = CondicionIvaOverride
            };

            // Percepción IIBB CABA (si aplica)
            if (AplicarPercepcionCaba && ImportePercepcionCaba > 0)
            {
                var baseArs = Proforma.ImporteGravado * (Proforma.CotizacionMoneda <= 0 ? 1m : Proforma.CotizacionMoneda);
                opciones.PercepcionIIBB = new PercepcionIIBB("CABA", baseArs, AlicuotaPercepcionCaba, ImportePercepcionCaba);
            }

            // Comprobante asociado (si aplica)
            if (RequiereAsociado && AsociadoNumero > 0)
            {
                opciones.ComprobanteAsociado = new ComprobanteAsociadoMapeo
                {
                    CodigoTipoComprobante = AsociadoTipo,
                    PuntoVenta = AsociadoPuntoVenta,
                    Numero = AsociadoNumero,
                    FechaEmision = AsociadoFecha is null ? null : DateOnly.FromDateTime(AsociadoFecha.Value),
                    CuitEmisor = AsociadoSeleccionado?.CuitEmisor,
                };
            }

            // Tipo de saldo cta cte (P/S) — sólo persistencia local
            opciones.TipoSaldo = RequiereTipoSaldo ? TipoSaldo : TipoSaldoCtaCte.Pesificado;

            // FCE — SCA/ADC + opcionales bancarios
            if (EsFce)
            {
                opciones.FceEsSCA = FceEsSCA;
                opciones.FceCbu = string.IsNullOrWhiteSpace(FceCbu) ? null : FceCbu.Trim();
                opciones.FceAlias = string.IsNullOrWhiteSpace(FceAlias) ? null : FceAlias.Trim();
            }

            // Override tipo comprobante: FC normal → FCE MiPyME
            if (ForzarComoFce && PuedeForzarFce)
            {
                var tipoOriginal = (TipoComprobante)Proforma.CodigoTipoComprobanteOrigen;
                opciones.TipoComprobanteOverride = tipoOriginal.EquivalenteFce();
            }

            var comprobante = mapeador.Ejecutar(Proforma, cfg, opciones);

            var resultado = await emisor.EjecutarAsync(comprobante);
            if (!resultado.Aprobada)
            {
                MensajeEstado = resultado.ErroresLocales is not null
                    ? "Validación falló:\n" + resultado.ErroresLocales.ResumenAmigable
                    : resultado.ErroresArca?.Message
                      ?? resultado.MotivoPendiente
                      ?? "Rechazado por ARCA.";
                return;
            }

            UltimoEmitido = resultado.Comprobante;
            Proforma.Estado = EstadoProforma.Autorizada;
            Proforma.ComprobanteId = resultado.Comprobante!.Id;
            await proformaRepo.UpdateAsync(Proforma);

            var pdfPath = await pdf.EjecutarAsync(resultado.Comprobante);
            MensajeEstado = $"CAE {resultado.Comprobante.Cae!.Numero} obtenido. PDF: {pdfPath}";

            await imprimir.EjecutarAsync(resultado.Comprobante);

            MoverProformaProcesada(Proforma, cfg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar factura electrónica.");
            MensajeEstado = $"Error: {ex.Message}";
            _notificaciones.Error(
                "Error inesperado",
                ex.Message,
                accionSugerida: "Revise el log para más detalles. Si persiste, contacte soporte técnico.");
        }
        finally
        {
            Emitiendo = false;
        }
    }

    private bool ValidarAsociado(out string motivo)
    {
        motivo = "";
        if (AsociadoTipo <= 0) { motivo = "Debés seleccionar el tipo del comprobante asociado."; return false; }
        if (AsociadoPuntoVenta <= 0) { motivo = "Punto de venta del comprobante asociado inválido."; return false; }
        if (AsociadoNumero <= 0) { motivo = "Número del comprobante asociado inválido."; return false; }
        return true;
    }
}
