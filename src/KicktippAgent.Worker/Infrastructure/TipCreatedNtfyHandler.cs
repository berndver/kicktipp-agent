using KicktippAgent.Worker.Configuration;
using KicktippAgent.Worker.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using TimeWarp.Mediator;

namespace KicktippAgent.Worker.Infrastructure;

public sealed class TipCreatedNtfyHandler : INotificationHandler<TipCreatedEvent>
{
    private readonly IOptions<NtfyOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TipCreatedNtfyHandler> _logger;

    public TipCreatedNtfyHandler(
        IOptions<NtfyOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<TipCreatedNtfyHandler> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Handle(TipCreatedEvent notification, CancellationToken ct)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(opts.Topic))
        {
            _logger.LogWarning("Ntfy is enabled but Topic is not configured");
            return;
        }

        var tip = notification.Tip;
        var message = $"WM 2026 \u2013 Neuer Tipp\n\n" +
                      $"{tip.Match.First.Name} vs {tip.Match.Second.Name}\n" +
                      $"Anstoss: {tip.Match.KickoffTime.ToLocalTime():dd.MM.yyyy HH:mm}\n\n" +
                      $"Tipp: {tip.HomeGoals}:{tip.AwayGoals}\n" +
                      $"Begründung: {tip.Reasoning}";

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{opts.Server.TrimEnd('/')}/{opts.Topic}";
            var content = new StringContent(message, System.Text.Encoding.UTF8, "text/plain");

            var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Ntfy notification sent to {Topic}", opts.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Ntfy notification to {Topic}", opts.Topic);
        }
    }
}
