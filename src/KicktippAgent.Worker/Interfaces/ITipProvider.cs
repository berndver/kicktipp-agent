using KicktippAgent.Worker.Domain;

namespace KicktippAgent.Worker.Interfaces;

public interface ITipProvider
{
    Task<Tip> GetTipAsync(Match match, CancellationToken ct = default);
}
