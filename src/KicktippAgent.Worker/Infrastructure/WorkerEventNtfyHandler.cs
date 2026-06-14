using JetBrains.Annotations;
using KicktippAgent.Worker.Configuration;
using KicktippAgent.Worker.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtfyCator;
using NtfyCator.Messages;
using TimeWarp.Mediator;

namespace KicktippAgent.Worker.Infrastructure;

[UsedImplicitly]
public sealed class WorkerEventNtfyHandler(
    INotificator notificator,
    IOptions<NtfyOptions> options,
    ILogger<WorkerEventNtfyHandler> logger
    )
    : INotificationHandler<WorkerFailedEvent>,
      INotificationHandler<ApplicationStartedEvent>
{
    public async Task Handle(WorkerFailedEvent notification, CancellationToken ct)
    {
        try
        {
            if (!options.Value.Enabled || string.IsNullOrWhiteSpace(options.Value.Topic))
                return;

            if (!string.IsNullOrWhiteSpace(options.Value.AccessToken))
                notificator.WithAccessToken(options.Value.AccessToken);

            var msg = new NtfyMessageBuilder(options.Value.Topic)
                .WithPriority(NtfyPriority.High)
                .WithTitle("Worker run failed")
                .WithMarkdownBody(
                    $"""
                     # Worker run failed

                     **{notification.ExceptionType}**: {notification.Message}
                     """
                )
                .WithTags("warning", "rotating_light")
                .Build();

            await notificator.NotifyAsync(msg, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Ntfy notification to {Topic}", options.Value.Topic);
        }
    }

    public async Task Handle(ApplicationStartedEvent notification, CancellationToken ct)
    {
        try
        {
            if (!options.Value.Enabled || string.IsNullOrWhiteSpace(options.Value.Topic))
                return;

            var msg = new NtfyMessageBuilder(options.Value.Topic)
                .WithTitle("Kicktipp Agent started")
                .WithMarkdownBody(
                    $"""
                     # Kicktipp Agent is online

                     - Group: **{notification.Group}**
                     - Model: **{notification.LlmModel}**
                     - Cron: **{notification.Cron}**
                     - Window: **{notification.Window}**
                     - MatchProvider: **{notification.MatchProvider}**
                     - TipSubmitter: **{notification.TipSubmitter}**
                     """
                )
                .WithTags("rocket", "checkered_flag")
                .Build();

            await notificator.NotifyAsync(msg, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Ntfy notification to {Topic}", options.Value.Topic);
        }
    }
}
