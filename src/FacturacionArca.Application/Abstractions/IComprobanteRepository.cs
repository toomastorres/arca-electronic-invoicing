using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Proformas;

namespace FacturacionArca.Application.Abstractions;

public interface IProformaRepository
{
    Task<int> AddAsync(ProformaNapoles proforma, CancellationToken ct = default);
    Task<ProformaNapoles?> FindByArchivoAsync(string archivoOrigen, CancellationToken ct = default);
    Task<ProformaNapoles?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProformaNapoles>> ListAsync(EstadoProforma? estado = null, CancellationToken ct = default);
    Task UpdateAsync(ProformaNapoles proforma, CancellationToken ct = default);
}

public interface IComprobanteRepository
{
    Task<int> AddAsync(Comprobante comprobante, CancellationToken ct = default);
    Task UpdateAsync(Comprobante comprobante, CancellationToken ct = default);
    Task<Comprobante?> GetAsync(int id, CancellationToken ct = default);
    Task<Comprobante?> FindByPtoVtaTipoNumeroAsync(int ptoVta, TipoComprobante tipo, long numero, CancellationToken ct = default);
    Task<IReadOnlyList<Comprobante>> SearchAsync(string? texto, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);
    /// <summary>Lista comprobantes con CAE (autorizados) emitidos entre dos fechas inclusive.</summary>
    Task<IReadOnlyList<Comprobante>> ListarAutorizadosEnRangoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);

    /// <summary>Lista los últimos N comprobantes autorizados de un receptor (por CUIT/documento), opcional filtro por tipo.</summary>
    Task<IReadOnlyList<Comprobante>> ListarPorReceptorAsync(string numeroDocumento, TipoComprobante? tipo = null, int max = 50, CancellationToken ct = default);
}

public interface ITicketAccesoCache
{
    Task<Domain.Wsaa.TicketAcceso?> GetAsync(string servicio, string cuit, string modo, CancellationToken ct = default);
    Task SaveAsync(Domain.Wsaa.TicketAcceso ticket, CancellationToken ct = default);
    Task RemoveAsync(string servicio, string cuit, string modo, CancellationToken ct = default);
}

public interface IConfiguracionRepository
{
    Task<Domain.Configuracion.ConfiguracionEmpresa> GetAsync(CancellationToken ct = default);
    Task SaveAsync(Domain.Configuracion.ConfiguracionEmpresa cfg, CancellationToken ct = default);
}
