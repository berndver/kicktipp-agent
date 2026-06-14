using KicktippAgent.Worker.Configuration;
using KicktippAgent.Worker.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using JetBrains.Annotations;
using NtfyCator;
using NtfyCator.Messages;
using TimeWarp.Mediator;

namespace KicktippAgent.Worker.Infrastructure;

[UsedImplicitly]
public sealed class TipCreatedNtfyHandler(
    INotificator notificator,
    IOptions<NtfyOptions> options,
    ILogger<TipCreatedNtfyHandler> logger
    )
    : INotificationHandler<TipCreatedEvent>
{
    public async Task Handle(TipCreatedEvent notification, CancellationToken ct)
    {
        try
        {
            if (!options.Value.Enabled || string.IsNullOrWhiteSpace(options.Value.Topic))
                return;

            var msg = new NtfyMessageBuilder(options.Value.Topic)
                .WithTitle("New bet placed")
                .WithMarkdownBody(
                    $"""
                     # {notification.Tip.Match.First.Name} vs {notification.Tip.Match.Second.Name} ({notification.Tip.HomeGoals}:{notification.Tip.AwayGoals})

                     {notification.Tip.Reasoning}
                     """
                )
                .Build();

            await notificator.NotifyAsync(msg, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Ntfy notification to {Topic}", options.Value.Topic);
        }
    }
}
