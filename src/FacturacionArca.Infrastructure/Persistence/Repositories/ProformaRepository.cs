using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Proformas;
using Microsoft.EntityFrameworkCore;

namespace FacturacionArca.Infrastructure.Persistence.Repositories;

public sealed class ProformaRepository : IProformaRepository
{
    private readonly FacturacionDbContext _db;
    public ProformaRepository(FacturacionDbContext db) => _db = db;

    public async Task<int> AddAsync(ProformaNapoles proforma, CancellationToken ct = default)
    {
        _db.Proformas.Add(proforma);
        await _db.SaveChangesAsync(ct);
        return proforma.Id;
    }

    public Task<ProformaNapoles?> FindByArchivoAsync(string archivoOrigen, CancellationToken ct = default) =>
        _db.Proformas
            .Include(p => p.Items)
            .Include(p => p.SubtotalesIva)
            .Include(p => p.ComprobanteAsociado)
            .FirstOrDefaultAsync(p => p.ArchivoOrigen == archivoOrigen, ct);

    public Task<ProformaNapoles?> GetAsync(int id, CancellationToken ct = default) =>
        _db.Proformas
            .Include(p => p.Items)
            .Include(p => p.SubtotalesIva)
            .Include(p => p.ComprobanteAsociado)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<ProformaNapoles>> ListAsync(EstadoProforma? estado = null, CancellationToken ct = default)
    {
        IQueryable<ProformaNapoles> q = _db.Proformas
            .Include(p => p.Items)
            .Include(p => p.SubtotalesIva)
            .OrderByDescending(p => p.FechaImportacion);
        if (estado is not null) q = q.Where(p => p.Estado == estado);
        return await q.ToListAsync(ct);
    }

    public async Task UpdateAsync(ProformaNapoles proforma, CancellationToken ct = default)
    {
        _db.Proformas.Update(proforma);
        await _db.SaveChangesAsync(ct);
    }
}
