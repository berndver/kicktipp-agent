using System.ComponentModel.DataAnnotations;

namespace KicktippAgent.Worker.Configuration;

public sealed class ScheduleOptions
{
    [Required]
    public string Cron { get; set; } = "0 * * * *";

    [Required]
    public string UpcomingWindow { get; set; } = "24:00";
}

public sealed class ProviderOptions
{
    [Required]
    public string Match { get; set; } = "kicktipp";

    [Required]
    public string TipSubmitter { get; set; } = "kicktipp";
}

public sealed class OpenAiOptions
{
    [Required]
    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "gpt-5.4-mini";

    /// <summary>
    /// Custom system prompt. Falls back to built-in default if empty.
    /// </summary>
    public string Preprompt { get; set; } = "";
}

public sealed class NtfyOptions
{
    public bool Enabled { get; set; } = false;

    public string Topic { get; set; } = "";

    public string Server { get; set; } = "https://ntfy.sh";
}
