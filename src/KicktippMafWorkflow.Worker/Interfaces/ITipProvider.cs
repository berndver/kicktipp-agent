namespace KicktippMafWorkflow.Worker;

public interface ITipProvider
{
    Task<Tip> GetTipAsync(Match match, CancellationToken ct = default);
}
