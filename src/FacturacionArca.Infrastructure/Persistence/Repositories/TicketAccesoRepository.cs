using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Domain.Wsaa;
using Microsoft.EntityFrameworkCore;

namespace FacturacionArca.Infrastructure.Persistence.Repositories;

public sealed class TicketAccesoCache : ITicketAccesoCache
{
    private readonly FacturacionDbContext _db;
    public TicketAccesoCache(FacturacionDbContext db) => _db = db;

    public Task<TicketAcceso?> GetAsync(string servicio, string cuit, string modo, CancellationToken ct = default) =>
        _db.TicketsAcceso.FirstOrDefaultAsync(t => t.Servicio == servicio && t.Cuit == cuit && t.Modo == modo, ct);

    public async Task SaveAsync(TicketAcceso ticket, CancellationToken ct = default)
    {
        var existente = await _db.TicketsAcceso.FirstOrDefaultAsync(
            t => t.Servicio == ticket.Servicio && t.Cuit == ticket.Cuit && t.Modo == ticket.Modo, ct);
        if (existente is null)
        {
            _db.TicketsAcceso.Add(ticket);
        }
        else
        {
            existente.Token = ticket.Token;
            existente.Sign = ticket.Sign;
            existente.GenerationTime = ticket.GenerationTime;
            existente.ExpirationTime = ticket.ExpirationTime;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string servicio, string cuit, string modo, CancellationToken ct = default)
    {
        var existente = await _db.TicketsAcceso.FirstOrDefaultAsync(
            t => t.Servicio == servicio && t.Cuit == cuit && t.Modo == modo, ct);
        if (existente is not null)
        {
            _db.TicketsAcceso.Remove(existente);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public sealed class ConfiguracionRepository : IConfiguracionRepository
{
    private readonly FacturacionDbContext _db;
    public ConfiguracionRepository(FacturacionDbContext db) => _db = db;

    public async Task<ConfiguracionEmpresa> GetAsync(CancellationToken ct = default)
    {
        var cfg = await _db.Configuraciones.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = ConstruirSemillaDefault();
            _db.Configuraciones.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    public async Task SaveAsync(ConfiguracionEmpresa cfg, CancellationToken ct = default)
    {
        if (cfg.Id == 0) _db.Configuraciones.Add(cfg);
        else _db.Configuraciones.Update(cfg);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Valores iniciales para el primer arranque. Se permite override via variables
    /// de entorno (FA_CUIT, FA_RAZON, FA_CERT_PATH, FA_CERT_PASS, FA_PTOVTA,
    /// FA_DIR_IN, FA_DIR_OUT, FA_DIR_AFIP, FA_DIR_PDF, FA_DOMICILIO).
    /// </summary>
    private static ConfiguracionEmpresa ConstruirSemillaDefault() => new()
    {
        Cuit = Environment.GetEnvironmentVariable("FA_CUIT") ?? "20111111112",
        RazonSocial = Environment.GetEnvironmentVariable("FA_RAZON") ?? "Razon Social Demo",
        DomicilioComercial = Environment.GetEnvironmentVariable("FA_DOMICILIO") ?? "",
        CondicionFrenteIva = "Responsable Inscripto",
        IngresosBrutos = "CM05",
        EsConvenioMultilateral = true,
        PuntoVentaPorDefecto = int.TryParse(Environment.GetEnvironmentVariable("FA_PTOVTA"), out var pv) ? pv : 1,
        Modo = ModoOperacion.Homologacion,
        CertificadoPath = Environment.GetEnvironmentVariable("FA_CERT_PATH") ?? @"C:\certificados_arca\empresa\empresa.pfx",
        CertificadoPassword = Environment.GetEnvironmentVariable("FA_CERT_PASS") ?? "",
        CarpetaProformas = Environment.GetEnvironmentVariable("FA_DIR_IN") ?? @"C:\AppFacturacion\InvoiceArgentina\Invoice\In",
        CarpetaProformasProcesadas = Environment.GetEnvironmentVariable("FA_DIR_OUT") ?? @"C:\AppFacturacion\InvoiceArgentina\Invoice\Out",
        CarpetaArchivoFiscal = Environment.GetEnvironmentVariable("FA_DIR_AFIP") ?? @"C:\AppFacturacion\InvoiceArgentina\Invoice\AFIP",
        CarpetaPdfSalida = Environment.GetEnvironmentVariable("FA_DIR_PDF") ?? @"C:\AppFacturacion\Facturas",
        CbuPesos = Environment.GetEnvironmentVariable("FA_CBU") ?? "",
        ImpresionSilenciosa = false,
    };
}
