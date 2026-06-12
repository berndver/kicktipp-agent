using KicktippMafWorkflow.Worker.Domain;

namespace KicktippMafWorkflow.Worker.Interfaces;

public interface IMatchProvider
{
    public Task<IEnumerable<Match>> GetUpcomingMatchAsync(DateTimeOffset limit);
}
