using KicktippAgent.Worker.Domain;

namespace KicktippAgent.Worker.Interfaces;

public interface IMatchProvider
{
    public Task<IEnumerable<Match>> GetUpcomingMatchAsync(DateTimeOffset limit);
}
