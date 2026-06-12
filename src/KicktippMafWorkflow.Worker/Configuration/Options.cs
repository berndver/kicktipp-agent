using System.ComponentModel.DataAnnotations;

namespace KicktippMafWorkflow.Worker;

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
