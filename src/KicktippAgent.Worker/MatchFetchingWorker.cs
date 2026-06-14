using Cronos;
using KicktippAgent.Worker.Configuration;
using KicktippAgent.Worker.Domain;
using KicktippAgent.Worker.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeWarp.Mediator;

namespace KicktippAgent.Worker;

public sealed class MatchFetchingWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ITipProvider _tipProvider;
    private readonly IOptions<ScheduleOptions> _scheduleOptions;
    private readonly IOptions<ProviderOptions> _providerOptions;
    private readonly ILogger<MatchFetchingWorker> _logger;

    public MatchFetchingWorker(
        IServiceProvider services,
        ITipProvider tipProvider,
        IOptions<ScheduleOptions> scheduleOptions,
        IOptions<ProviderOptions> providerOptions,
        ILogger<MatchFetchingWorker> logger)
    {
        _services = services;
        _tipProvider = tipProvider;
        _scheduleOptions = scheduleOptions;
        _providerOptions = providerOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cron = CronExpression.Parse(_scheduleOptions.Value.Cron, CronFormat.Standard);
        var window = ParseUpcomingWindow(_scheduleOptions.Value.UpcomingWindow);

        _logger.LogInformation("Cron: {Cron}, UpcomingWindow: {Window}", _scheduleOptions.Value.Cron, window);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var nextRun = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);
            if (nextRun is null)
            {
                _logger.LogWarning("Cron expression '{Cron}' yields no future occurrences", _scheduleOptions.Value.Cron);
                break;
            }

            var delay = nextRun.Value - now;
            _logger.LogInformation("Next run at {NextRun:yyyy-MM-dd HH:mm:ss} UTC (in {Delay})", nextRun.Value, delay);
            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            await ExecuteWorkflowAsync(window, stoppingToken);
        }
    }

    private async Task ExecuteWorkflowAsync(TimeSpan window, CancellationToken stoppingToken)
    {
        try
        {
            var limit = DateTimeOffset.UtcNow + window;
            _logger.LogInformation("Searching for untipped matches up to {Limit:dd.MM.yyyy HH:mm} UTC", limit);

            var matchProvider = _services.GetRequiredKeyedService<IMatchProvider>(_providerOptions.Value.Match);
            var matches = await matchProvider.GetUpcomingMatchAsync(limit);

            if (!matches.Any())
            {
                _logger.LogInformation("No untipped matches found.");
                return;
            }

            _logger.LogInformation("{Count} untipped match(es):", matches.Count());

            var tips = new List<Tip>();
            foreach (var match in matches)
            {
                var countdown = match.KickoffTime - DateTimeOffset.UtcNow;
                _logger.LogInformation(
                    "  {Home} vs {Away} | Kickoff: {Kickoff:dd.MM.yyyy HH:mm} (in {Days}d {Hours}h {Minutes}m)",
                    match.First.Name, match.Second.Name,
                    match.KickoffTime.ToLocalTime(),
                    countdown.Days, countdown.Hours, countdown.Minutes);

                var tip = await _tipProvider.GetTipAsync(match, stoppingToken);
                _logger.LogInformation(
                    "  => Tip: {HomeGoals}:{AwayGoals} | {Reasoning}",
                    tip.HomeGoals, tip.AwayGoals, tip.Reasoning);

                tips.Add(tip);

                await using var scope = _services.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Publish(new TipCreatedEvent(tip), stoppingToken);
            }

            _logger.LogInformation("Submitting {Count} tip(s)...", tips.Count);
            var tipSubmitter = _services.GetRequiredKeyedService<ITipSubmitter>(_providerOptions.Value.TipSubmitter);
            await tipSubmitter.SubmitAsync(tips);

            _logger.LogInformation("Done.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching matches");

            await using var scope = _services.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Publish(new WorkerFailedEvent(ex.Message, ex.GetType().Name), CancellationToken.None);
        }
    }

    private static TimeSpan ParseUpcomingWindow(string value)
    {
        var parts = value.Split(':');
        if (parts.Length < 2
            || !int.TryParse(parts[0], out var hours)
            || !int.TryParse(parts[1], out var minutes))
            throw new FormatException($"Invalid UpcomingWindow format '{value}'. Expected 'hh:mm'.");

        return new TimeSpan(hours, minutes, 0);
    }
}
