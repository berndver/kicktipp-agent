namespace KicktippMafWorkflow.Worker;

public interface IMatchProvider
{
    public Task<IEnumerable<Match>> GetUpcomingMatchAsync(DateTimeOffset limit);
}
