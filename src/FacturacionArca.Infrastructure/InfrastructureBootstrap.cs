using FacturacionArca.Application.Abstractions;
using FacturacionArca.Application.UseCases;
using FacturacionArca.Application.Validacion;
using FacturacionArca.Infrastructure.Arca.Wsaa;
using FacturacionArca.Infrastructure.Padrones;
using FacturacionArca.Infrastructure.Arca.Wsfecred;
using FacturacionArca.Infrastructure.Arca.Wsfev1;
using FacturacionArca.Infrastructure.FileWatching;
using FacturacionArca.Infrastructure.Pdf;
using FacturacionArca.Infrastructure.Persistence;
using FacturacionArca.Infrastructure.Persistence.Repositories;
using FacturacionArca.Infrastructure.Printing;
using FacturacionArca.Infrastructure.XmlNapoles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FacturacionArca.Infrastructure;

public static class InfrastructureBootstrap
{
    public static IServiceCollection AddFacturacionArcaInfrastructure(
        this IServiceCollection services, string sqlitePath)
    {
        services.AddDbContext<FacturacionDbContext>(opt =>
            opt.UseSqlite($"Data Source={sqlitePath}"));

        services.AddScoped<IProformaRepository, ProformaRepository>();
        services.AddScoped<IComprobanteRepository, ComprobanteRepository>();
        services.AddScoped<ITicketAccesoCache, TicketAccesoCache>();
        services.AddScoped<IConfiguracionRepository, ConfiguracionRepository>();
        services.AddScoped<IArchivoFiscalRepository, ArchivoFiscalRepository>();
        services.AddScoped<IPadronIibbRepository, PadronIibbRepository>();
        services.AddSingleton<IPadronAgipParserService, PadronAgipParserService>();

        services.AddSingleton<IProformaXmlParser, ProformaXmlParser>();
        services.AddSingleton<ICertificadoProvider, FileSystemCertificadoProvider>();
        services.AddSingleton<IQrAfipBuilder, QrAfipBuilder>();
        services.AddSingleton<IPdfRenderer, MigraDocPdfRenderer>();
        services.AddSingleton<IPrinterService, SilentPdfPrinter>();
        services.AddSingleton<IProformaWatcher, ProformaFileWatcher>();

        services.AddSingleton<INotificacionService, NotificacionService>();

        services.AddHttpClient<IArcaWsaaClient, WsaaClient>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient<IArcaWsfev1Client, Wsfev1Client>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddScoped<IArcaWsfecredClient, WsfecredClient>();

        services.AddScoped<ValidadorAritmeticoAfip>();
        services.AddScoped<ValidadorReglasNegocio>();

        services.AddScoped<ImportarProformaXml>();
        services.AddScoped<MapearProformaAComprobante>();
        services.AddScoped<EmitirComprobante>();
        services.AddScoped<ResolverContingenciaRed>();
        services.AddScoped<GenerarPdfComprobante>();
        services.AddScoped<ImprimirComprobante>();
        services.AddScoped<SincronizarMaestrosArca>();
        services.AddScoped<ImportarPadronAgip>();
        services.AddScoped<CalcularPercepcionIibbCaba>();
        services.AddScoped<EscribirCallbackOut>();
        services.AddScoped<ExportarLibroIvaVentas>();
        services.AddScoped<ExportarAgipPercepcionesTxt>();

        return services;
    }

    public static async Task InicializarBaseAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FacturacionDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        // Forzar creación de la configuración inicial vía el repositorio
        // (que ya tiene la lógica de semilla con defaults / variables de entorno).
        var configRepo = scope.ServiceProvider.GetRequiredService<IConfiguracionRepository>();
        _ = await configRepo.GetAsync(ct);
    }
}
