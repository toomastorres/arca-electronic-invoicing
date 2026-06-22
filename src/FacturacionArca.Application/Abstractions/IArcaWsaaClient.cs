using FacturacionArca.Domain.Wsaa;

namespace FacturacionArca.Application.Abstractions;

public interface IArcaWsaaClient
{
    Task<TicketAcceso> ObtenerTicketAsync(string servicio, CancellationToken ct = default);
    Task<TicketAcceso> RenovarTicketAsync(string servicio, CancellationToken ct = default);
}
