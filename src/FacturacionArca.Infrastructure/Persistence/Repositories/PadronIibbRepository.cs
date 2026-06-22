using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Padrones;
using Microsoft.EntityFrameworkCore;

namespace FacturacionArca.Infrastructure.Persistence.Repositories;

public sealed class PadronIibbRepository : IPadronIibbRepository
{
    private readonly FacturacionDbContext _db;
    public PadronIibbRepository(FacturacionDbContext db) => _db = db;

    public async Task<int> ReemplazarLoteAsync(
        DateOnly fechaPublicacion,
        IAsyncEnumerable<PadronIibbCaba> entradas,
        CancellationToken ct = default)
    {
        // Borrar el lote previo de la misma fecha pub para evitar duplicados
        await _db.PadronIibbCaba
            .Where(p => p.FechaPublicacion == fechaPublicacion)
            .ExecuteDeleteAsync(ct);

        const int batchSize = 5_000;
        var buffer = new List<PadronIibbCaba>(batchSize);
        var total = 0;

        await foreach (var e in entradas.WithCancellation(ct))
        {
            buffer.Add(e);
            if (buffer.Count >= batchSize)
            {
                await GuardarBufferAsync(buffer, ct);
                total += buffer.Count;
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
        {
            await GuardarBufferAsync(buffer, ct);
            total += buffer.Count;
        }
        return total;
    }

    private async Task GuardarBufferAsync(List<PadronIibbCaba> buffer, CancellationToken ct)
    {
        _db.PadronIibbCaba.AddRange(buffer);
        await _db.SaveChangesAsync(ct);
        // Limpiar tracking para no acumular memoria
        foreach (var entry in _db.ChangeTracker.Entries<PadronIibbCaba>().ToList())
            entry.State = EntityState.Detached;
    }

    public Task<PadronIibbCaba?> BuscarVigenteAsync(string cuit, DateOnly? a = null, CancellationToken ct = default)
    {
        var fecha = a ?? DateOnly.FromDateTime(DateTime.Today);
        return _db.PadronIibbCaba
            .Where(p => p.Cuit == cuit && p.VigenciaDesde <= fecha && p.VigenciaHasta >= fecha)
            .OrderByDescending(p => p.FechaPublicacion)
            .FirstOrDefaultAsync(ct);
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _db.PadronIibbCaba.CountAsync(ct);

    public async Task<DateOnly?> UltimaFechaPublicacionAsync(CancellationToken ct = default)
    {
        var max = await _db.PadronIibbCaba.MaxAsync(p => (DateOnly?)p.FechaPublicacion, ct);
        return max;
    }
}
