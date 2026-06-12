namespace KicktippMafWorkflow.Worker;

public interface ITipSubmitter
{
    public Task SubmitAsync(IEnumerable<Tip> tips);
}
