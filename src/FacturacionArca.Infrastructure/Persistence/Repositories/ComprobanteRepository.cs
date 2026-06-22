using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using Microsoft.EntityFrameworkCore;

namespace FacturacionArca.Infrastructure.Persistence.Repositories;

public sealed class ComprobanteRepository : IComprobanteRepository
{
    private readonly FacturacionDbContext _db;
    public ComprobanteRepository(FacturacionDbContext db) => _db = db;

    public async Task<int> AddAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        _db.Comprobantes.Add(comprobante);
        await _db.SaveChangesAsync(ct);
        return comprobante.Id;
    }

    public async Task UpdateAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        _db.Comprobantes.Update(comprobante);
        await _db.SaveChangesAsync(ct);
    }

    public Task<Comprobante?> GetAsync(int id, CancellationToken ct = default) =>
        _db.Comprobantes
            .Include(c => c.Receptor)
            .Include(c => c.Items)
            .Include(c => c.SubtotalesIva)
            .Include(c => c.Tributos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Comprobante?> FindByPtoVtaTipoNumeroAsync(int ptoVta, TipoComprobante tipo, long numero, CancellationToken ct = default) =>
        _db.Comprobantes
            .Include(c => c.Receptor)
            .Include(c => c.Items)
            .Include(c => c.SubtotalesIva)
            .Include(c => c.Tributos)
            .FirstOrDefaultAsync(c => c.PuntoVenta == ptoVta && c.Tipo == tipo && c.Numero == numero, ct);

    public async Task<IReadOnlyList<Comprobante>> SearchAsync(string? texto, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        IQueryable<Comprobante> q = _db.Comprobantes
            .Include(c => c.Receptor)
            .OrderByDescending(c => c.FechaEmision)
            .ThenByDescending(c => c.Id);

        if (desde is not null) q = q.Where(c => c.FechaEmision >= desde);
        if (hasta is not null) q = q.Where(c => c.FechaEmision <= hasta);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = q.Where(c =>
                c.Receptor.RazonSocial.Contains(t) ||
                c.Receptor.NumeroDocumento.Contains(t) ||
                c.ProformaOrigen.Contains(t));
        }

        return await q.Take(500).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Comprobante>> ListarAutorizadosEnRangoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        return await _db.Comprobantes
            .Include(c => c.Receptor)
            .Include(c => c.Items)
            .Include(c => c.SubtotalesIva)
            .Include(c => c.Tributos)
            .Include(c => c.ComprobantesAsociados)
            .Where(c => c.FechaEmision >= desde && c.FechaEmision <= hasta && c.Cae != null)
            .OrderBy(c => c.FechaEmision)
            .ThenBy(c => c.PuntoVenta)
            .ThenBy(c => c.Numero)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Comprobante>> ListarPorReceptorAsync(string numeroDocumento, TipoComprobante? tipo = null, int max = 50, CancellationToken ct = default)
    {
        IQueryable<Comprobante> q = _db.Comprobantes
            .Include(c => c.Receptor)
            .Where(c => c.Receptor.NumeroDocumento == numeroDocumento && c.Cae != null);

        if (tipo is not null) q = q.Where(c => c.Tipo == tipo);

        return await q.OrderByDescending(c => c.FechaEmision)
                      .ThenByDescending(c => c.Numero)
                      .Take(max)
                      .ToListAsync(ct);
    }
}
