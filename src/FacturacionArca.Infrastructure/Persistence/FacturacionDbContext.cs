using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using FacturacionArca.Domain.Padrones;
using FacturacionArca.Domain.Proformas;
using FacturacionArca.Domain.Wsaa;
using Microsoft.EntityFrameworkCore;

namespace FacturacionArca.Infrastructure.Persistence;

public sealed class FacturacionDbContext : DbContext
{
    public FacturacionDbContext(DbContextOptions<FacturacionDbContext> options) : base(options) { }

    public DbSet<ProformaNapoles> Proformas => Set<ProformaNapoles>();
    public DbSet<ItemProforma> ItemsProforma => Set<ItemProforma>();
    public DbSet<SubtotalIvaProforma> SubtotalesIvaProforma => Set<SubtotalIvaProforma>();
    public DbSet<ComprobanteAsociadoProforma> ComprobantesAsociadosProforma => Set<ComprobanteAsociadoProforma>();

    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();
    public DbSet<ItemComprobante> ItemsComprobante => Set<ItemComprobante>();
    public DbSet<SubtotalIva> SubtotalesIva => Set<SubtotalIva>();
    public DbSet<Tributo> Tributos => Set<Tributo>();
    public DbSet<ReceptorComprobante> Receptores => Set<ReceptorComprobante>();

    public DbSet<ComprobanteAsociado> ComprobantesAsociados => Set<ComprobanteAsociado>();

    public DbSet<TicketAcceso> TicketsAcceso => Set<TicketAcceso>();
    public DbSet<ConfiguracionEmpresa> Configuraciones => Set<ConfiguracionEmpresa>();
    public DbSet<Jurisdiccion> Jurisdicciones => Set<Jurisdiccion>();
    public DbSet<LogLlamadaArca> LogLlamadasArca => Set<LogLlamadaArca>();
    public DbSet<PadronIibbCaba> PadronIibbCaba => Set<PadronIibbCaba>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ProformaNapoles>(e =>
        {
            e.ToTable("Proformas");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ArchivoOrigen).IsUnique();
            e.HasIndex(x => x.NumeroProforma);
            e.HasMany(x => x.Items).WithOne().HasForeignKey("ProformaId").OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.SubtotalesIva).WithOne().HasForeignKey("ProformaId").OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ComprobanteAsociado).WithOne().HasForeignKey<ComprobanteAsociadoProforma>("ProformaId").OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Estado).HasConversion<int>();
        });

        b.Entity<ItemProforma>().ToTable("ProformaItems").HasKey(x => x.Id);
        b.Entity<SubtotalIvaProforma>().ToTable("ProformaSubtotalesIva").HasKey(x => x.Id);
        b.Entity<ComprobanteAsociadoProforma>().ToTable("ProformaComprobanteAsociado").HasKey(x => x.Id);

        b.Entity<Comprobante>(e =>
        {
            e.ToTable("Comprobantes");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PuntoVenta, x.Tipo, x.Numero });
            e.Property(x => x.Tipo).HasConversion<int>();
            e.Property(x => x.Concepto).HasConversion<int>();
            e.Property(x => x.CondicionVenta).HasConversion<int>();
            e.HasOne(x => x.Receptor).WithOne().HasForeignKey<ReceptorComprobante>("ComprobanteId").OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Items).WithOne().HasForeignKey("ComprobanteId").OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.SubtotalesIva).WithOne().HasForeignKey("ComprobanteId").OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Tributos).WithOne().HasForeignKey("ComprobanteId").OnDelete(DeleteBehavior.Cascade);
            e.OwnsOne(x => x.Cae, cae =>
            {
                cae.Property(c => c.Numero).HasColumnName("CaeNumero");
                cae.Property(c => c.FechaVencimiento).HasColumnName("CaeVencimiento");
                cae.Property(c => c.FechaProceso).HasColumnName("CaeFechaProceso");
            });
        });

        b.Entity<ItemComprobante>().ToTable("ComprobanteItems").HasKey(x => x.Id);
        b.Entity<SubtotalIva>().ToTable("ComprobanteSubtotalesIva").HasKey(x => x.Id);
        b.Entity<Tributo>().ToTable("ComprobanteTributos").HasKey(x => x.Id);
        b.Entity<ComprobanteAsociado>(e =>
        {
            e.ToTable("ComprobantesAsociados");
            e.HasKey(x => x.Id);
            e.HasOne<Comprobante>().WithMany(c => c.ComprobantesAsociados).HasForeignKey(x => x.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ReceptorComprobante>(e =>
        {
            e.ToTable("ComprobanteReceptores");
            e.HasKey(x => x.Id);
            e.Property(x => x.CondicionIva).HasConversion<int>();
        });

        b.Entity<TicketAcceso>(e =>
        {
            e.ToTable("TicketsAcceso");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Servicio, x.Cuit, x.Modo }).IsUnique();
        });

        b.Entity<ConfiguracionEmpresa>(e =>
        {
            e.ToTable("Configuracion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Modo).HasConversion<int>();
        });

        b.Entity<Jurisdiccion>().ToTable("Jurisdicciones").HasKey(x => x.Id);
        b.Entity<LogLlamadaArca>().ToTable("LogLlamadasArca").HasKey(x => x.Id);

        b.Entity<PadronIibbCaba>(e =>
        {
            e.ToTable("PadronIibbCaba");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Cuit);
            e.HasIndex(x => new { x.Cuit, x.VigenciaDesde });
        });

    }
}

public sealed class LogLlamadaArca
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Servicio { get; set; } = "";
    public string Metodo { get; set; } = "";
    public string? RequestXml { get; set; }
    public string? ResponseXml { get; set; }
    public int? CodigoErrorPrincipal { get; set; }
    public long DurationMs { get; set; }
    public bool Exito { get; set; }
}
