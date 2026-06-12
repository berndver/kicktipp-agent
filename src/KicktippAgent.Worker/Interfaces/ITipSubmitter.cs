using KicktippAgent.Worker.Domain;

namespace KicktippAgent.Worker.Interfaces;

public interface ITipSubmitter
{
    public Task SubmitAsync(IEnumerable<Tip> tips);
}
