using System.Globalization;
using KicktippAgent.Worker.Domain;
using KicktippAgent.Worker.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace KicktippAgent.Worker.Infrastructure;

public sealed class KicktippMatchProvider : IMatchProvider
{
    private const string LoginUrl = "/info/profil/login";
    private const string TippabgabePath = "/tippabgabe";

    private readonly IConfiguration _configuration;
    private readonly ILogger<KicktippMatchProvider> _logger;

    public KicktippMatchProvider(IConfiguration configuration, ILogger<KicktippMatchProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<Match>> GetUpcomingMatchAsync(DateTimeOffset limit)
    {
        var options = CreateChromeOptions();

        using var driver = new ChromeDriver(options);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

        try
        {
            var baseUrl = GetBaseUrl();
            var groupName = GetGroupName();

            await LoginAsync(driver, wait, baseUrl);

            var tippabgabeUrl = $"{baseUrl}/{groupName}{TippabgabePath}";
            _logger.LogInformation("Navigating to {Url}", tippabgabeUrl);
            driver.Navigate().GoToUrl(tippabgabeUrl);

            WaitForContent(wait);

            var spieltag = driver.FindElement(By.CssSelector(".prevnextTitle a")).Text;
            _logger.LogInformation("Matchday: {Spieltag}", spieltag);

            var (total, upcomingMatches) = ParseMatches(driver);
            _logger.LogInformation("{Total} matches total, {Upcoming} upcoming and untipped",
                total, upcomingMatches.Count);

            var upcoming = upcomingMatches
                .Where(m => m.KickoffTime >= DateTimeOffset.UtcNow && m.KickoffTime <= limit)
                .ToList();
            _logger.LogInformation("Of those {Count} within limit ({Limit:dd.MM.yyyy})",
                upcoming.Count, limit);

            return upcoming;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch matches from kicktipp");
            return [];
        }
    }

    private static ChromeOptions CreateChromeOptions()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        return options;
    }

    private async Task LoginAsync(WebDriver driver, WebDriverWait wait, string baseUrl)
    {
        var email = _configuration["Kicktipp:Email"]
            ?? throw new InvalidOperationException("Kicktipp:Email is not configured");
        var password = _configuration["Kicktipp:Password"]
            ?? throw new InvalidOperationException("Kicktipp:Password is not configured");

        var loginUrl = $"{baseUrl}{LoginUrl}";
        _logger.LogInformation("Logging in at {Url}", loginUrl);
        driver.Navigate().GoToUrl(loginUrl);

        var emailField = wait.Until(d => d.FindElement(By.Id("kennung")));
        emailField.Clear();
        emailField.SendKeys(email);

        var passwordField = driver.FindElement(By.Id("passwort"));
        passwordField.Clear();
        passwordField.SendKeys(password);

        driver.FindElement(By.Name("submitbutton")).Click();

        await Task.Delay(2000);
        wait.Until(d => !d.Url.Contains("login", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("Login successful");
    }

    private static void WaitForContent(WebDriverWait wait)
    {
        wait.Until(d =>
        {
            try
            {
                var el = d.FindElement(By.CssSelector("#tippabgabeSpiele tbody tr.datarow"));
                return el is not null;
            }
            catch
            {
                return false;
            }
        });
    }

    private static (int Total, List<Match> Upcoming) ParseMatches(WebDriver driver)
    {
        var rows = driver.FindElements(By.CssSelector("#tippabgabeSpiele tbody tr.datarow"));
        var total = rows.Count;
        var matches = new List<Match>();

        foreach (var row in rows)
        {
            var match = TryParseMatch(row);
            if (match is not null)
                matches.Add(match);
        }

        return (total, matches);
    }

    private static Match? TryParseMatch(IWebElement row)
    {
        try
        {
            var dateText = row.FindElement(By.CssSelector("td.kicktipp-time")).Text.Trim();
            var team1 = row.FindElement(By.CssSelector("td.col1")).Text.Trim();
            var team2 = row.FindElement(By.CssSelector("td.col2")).Text.Trim();

            if (string.IsNullOrWhiteSpace(team1) || string.IsNullOrWhiteSpace(team2))
                return null;

            if (IsAlreadyTipped(row))
                return null;

            var kickoffTime = ParseGermanDateTime(dateText);

            return new Match(new Team(team1), new Team(team2), kickoffTime);
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    private static bool IsAlreadyTipped(IWebElement row)
    {
        try
        {
            var tippCell = row.FindElement(By.CssSelector("td.col3"));
            var cls = tippCell.GetAttribute("class") ?? "";

            // nichttippbar matches can't be tipped at all (deadline passed)
            if (cls.Contains("nichttippbar"))
                return true;

            // Check if either tip input has a value already set
            var inputs = tippCell.FindElements(By.CssSelector("input[type='text']"));
            return inputs.Any(i => !string.IsNullOrEmpty(i.GetAttribute("value")));
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }

    private static DateTimeOffset ParseGermanDateTime(string text)
    {
        // Format: "dd.MM.yy HH:mm" e.g. "11.06.26 21:00"
        if (DateTimeOffset.TryParseExact(text, "dd.MM.yy HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result))
            return result;

        // Fallback: "dd.MM.yyyy HH:mm"
        if (DateTimeOffset.TryParseExact(text, "dd.MM.yyyy HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
            return result;

        // Try the generic parser with German culture
        var germanCulture = new CultureInfo("de-DE");
        if (DateTimeOffset.TryParse(text, germanCulture, DateTimeStyles.None, out result))
            return result;

        return DateTimeOffset.MinValue;
    }

    private string GetBaseUrl() =>
        _configuration["Kicktipp:BaseUrl"] ?? "https://www.kicktipp.de";

    private string GetGroupName() =>
        _configuration["Kicktipp:GroupName"]
            ?? throw new InvalidOperationException("Kicktipp:GroupName is not configured");
}
