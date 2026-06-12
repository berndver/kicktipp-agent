using KicktippMafWorkflow.Worker.Domain;

namespace KicktippMafWorkflow.Worker.Interfaces;

public interface ITipProvider
{
    Task<Tip> GetTipAsync(Match match, CancellationToken ct = default);
}
