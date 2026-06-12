using KicktippAgent.Worker;
using KicktippAgent.Worker.Configuration;
using KicktippAgent.Worker.Infrastructure;
using KicktippAgent.Worker.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var baseDir = AppContext.BaseDirectory;

var builder = Host.CreateApplicationBuilder(args);
builder.Environment.ContentRootPath = baseDir;

builder.Configuration
    .AddDotEnv(Path.Combine(baseDir, ".env"))
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOptions<ScheduleOptions>()
    .Bind(builder.Configuration.GetSection("Schedule"))
    .ValidateDataAnnotations();

builder.Services.AddOptions<ProviderOptions>()
    .Bind(builder.Configuration.GetSection("Provider"))
    .ValidateDataAnnotations();

builder.Services.AddOptions<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection("OpenAI"))
    .ValidateDataAnnotations();

builder.Services.AddKeyedSingleton<IMatchProvider, KicktippMatchProvider>("kicktipp");
builder.Services.AddKeyedSingleton<ITipSubmitter, KicktippTipSubmitter>("kicktipp");
builder.Services.AddSingleton<ITipProvider, OpenAiTipProvider>();
builder.Services.AddHostedService<MatchFetchingWorker>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<KicktippAgent.Worker.Program>>();
var cfg = host.Services.GetRequiredService<IConfiguration>();
var schedule = host.Services.GetRequiredService<IOptions<ScheduleOptions>>().Value;
var provider = host.Services.GetRequiredService<IOptions<ProviderOptions>>().Value;
var openAi = host.Services.GetRequiredService<IOptions<OpenAiOptions>>().Value;
logger.LogInformation("Group: {Group}, Model: {Model}, Cron: {Cron}, UpcomingWindow: {Window}, MatchProvider: {Match}, TipSubmitter: {Submitter}",
    cfg["Kicktipp:GroupName"], openAi.Model,
    schedule.Cron, schedule.UpcomingWindow,
    provider.Match, provider.TipSubmitter);

await host.RunAsync();

namespace KicktippAgent.Worker
{
    internal partial class Program;
}
