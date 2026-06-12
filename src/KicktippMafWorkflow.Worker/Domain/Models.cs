namespace KicktippMafWorkflow.Worker;

public record Team(string Name);

public record Match(Team First, Team Second, DateTimeOffset KickoffTime);

public record Tip(Match Match, int HomeGoals, int AwayGoals, string? Reasoning);
