using KicktippAgent.Worker.Configuration;
using KicktippAgent.Worker.Domain;
using KicktippAgent.Worker.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KicktippAgent.Worker.Infrastructure;

public sealed class OpenAiTipProvider : ITipProvider
{
    private const string DefaultPreprompt =
        "Du bist ein Fussball-Tipp-Experte und tippst Spiele der WM 2026. " +
        "Dein Ziel ist es, die Tipprunde zu gewinnen. Deshalb müssen deine Tipps stets gewissenhaft, " +
        "durchdacht und datenbasiert sein. Analysiere jede Begegnung anhand der Team-Namen " +
        "und gib einen fundierten, begründeten Tipp ab. Berücksichtige typische Stärken und " +
        "Schwächen der Teams, historische Leistungen, die Turniersituation und den Spielort. " +
        "Wäge Risiken ab und vermeide zu riskante Aussenseitertipps ohne solide Grundlage. " +
        "Halte deine Begründung kurz (max. 2–3 Sätze). " +
        "Antworte ausschliesslich im YAML-Format mit den Feldern homeGoals (int), awayGoals (int) " +
        "und reasoning (string). Keine Einleitung, kein Markdown-Codeblock, nur reines YAML.";

    private readonly IOptions<OpenAiOptions> _options;
    private readonly ILogger<OpenAiTipProvider> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public OpenAiTipProvider(IOptions<OpenAiOptions> options, ILogger<OpenAiTipProvider> logger)
    {
        _options = options;
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public async Task<Tip> GetTipAsync(Match match, CancellationToken ct = default)
    {
        var apiKey = _options.Value.ApiKey;
        var model = _options.Value.Model;
        var preprompt = string.IsNullOrWhiteSpace(_options.Value.Preprompt)
            ? DefaultPreprompt
            : _options.Value.Preprompt;

        var client = new OpenAIClient(apiKey);
        var chat = client.GetChatClient(model);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(preprompt),
            ChatMessage.CreateUserMessage(
                $"Spiel: {match.First.Name} gegen {match.Second.Name}\n" +
                $"Anstoss: {match.KickoffTime:dd.MM.yyyy HH:mm} Uhr")
        };

        _logger.LogInformation("  Asking LLM for tip: {Home} vs {Away} (Kickoff: {Kickoff:dd.MM.yyyy HH:mm})",
            match.First.Name, match.Second.Name, match.KickoffTime.ToLocalTime());

        var completion = await chat.CompleteChatAsync(messages, cancellationToken: ct);
        var content = completion.Value.Content[0].Text.Trim();

        _logger.LogDebug("LLM response:\n{Response}", content);

        var result = _yamlDeserializer.Deserialize<TipResult>(content)
            ?? throw new InvalidOperationException("Failed to parse YAML response");

        return new Tip(match, result.HomeGoals, result.AwayGoals, result.Reasoning);
    }

    private sealed class TipResult
    {
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public string? Reasoning { get; set; }
    }
}
