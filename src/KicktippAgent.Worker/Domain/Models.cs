using TimeWarp.Mediator;

namespace KicktippAgent.Worker.Domain;

public record Team(string Name);

public record Match(Team First, Team Second, DateTimeOffset KickoffTime);

public record Tip(Match Match, int HomeGoals, int AwayGoals, string? Reasoning);

public record TipCreatedEvent(Tip Tip) : INotification;

public record WorkerFailedEvent(string Message, string ExceptionType) : INotification;

public record ApplicationStartedEvent(
    string Group, string LlmModel, string Cron, string Window,
    string MatchProvider, string TipSubmitter
) : INotification;
