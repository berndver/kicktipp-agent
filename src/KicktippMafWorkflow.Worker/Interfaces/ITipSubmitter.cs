using KicktippMafWorkflow.Worker.Domain;

namespace KicktippMafWorkflow.Worker.Interfaces;

public interface ITipSubmitter
{
    public Task SubmitAsync(IEnumerable<Tip> tips);
}
