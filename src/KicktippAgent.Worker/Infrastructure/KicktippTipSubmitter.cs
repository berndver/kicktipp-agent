using KicktippAgent.Worker.Domain;
using KicktippAgent.Worker.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace KicktippAgent.Worker.Infrastructure;

public sealed class KicktippTipSubmitter : ITipSubmitter
{
    private const string LoginUrl = "/info/profil/login";
    private const string TippabgabePath = "/tippabgabe";

    private readonly IConfiguration _configuration;
    private readonly ILogger<KicktippTipSubmitter> _logger;

    public KicktippTipSubmitter(IConfiguration configuration, ILogger<KicktippTipSubmitter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SubmitAsync(IEnumerable<Tip> tips)
    {
        var tipList = tips.ToList();
        if (tipList.Count == 0)
        {
            _logger.LogInformation("No tips to submit.");
            return;
        }

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

            var submitted = 0;
            var rows = driver.FindElements(By.CssSelector("#tippabgabeSpiele tbody tr.datarow"));

            foreach (var row in rows)
            {
                var tip = FindMatchingTip(row, tipList);
                if (tip is null)
                    continue;

                FillTip(row, tip);
                submitted++;
                _logger.LogInformation("Filled tip {HomeGoals}:{AwayGoals} for {Home} vs {Away}",
                    tip.HomeGoals, tip.AwayGoals,
                    tip.Match.First.Name, tip.Match.Second.Name);
            }

            if (submitted > 0)
            {
                _logger.LogInformation("Submitting {Count} tip(s)...", submitted);
                ClickSubmitButton(driver);
                _logger.LogInformation("Tips submitted successfully.");
            }
            else
            {
                _logger.LogInformation("No matching rows found for the given tips.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit tips to kicktipp");
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

    private static Tip? FindMatchingTip(IWebElement row, List<Tip> tips)
    {
        try
        {
            var team1 = row.FindElement(By.CssSelector("td.col1")).Text.Trim();
            var team2 = row.FindElement(By.CssSelector("td.col2")).Text.Trim();

            // Check if this row is tippable (has text inputs in col3)
            var tippCell = row.FindElement(By.CssSelector("td.col3"));
            var cls = tippCell.GetAttribute("class") ?? "";
            if (cls.Contains("nichttippbar"))
                return null;

            return tips.FirstOrDefault(t =>
                string.Equals(t.Match.First.Name, team1, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Match.Second.Name, team2, StringComparison.OrdinalIgnoreCase));
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    private static void FillTip(IWebElement row, Tip tip)
    {
        var homeInput = row.FindElement(By.CssSelector("input[id$='_heimTipp']"));
        homeInput.Clear();
        homeInput.SendKeys(tip.HomeGoals.ToString());

        var awayInput = row.FindElement(By.CssSelector("input[id$='_gastTipp']"));
        awayInput.Clear();
        awayInput.SendKeys(tip.AwayGoals.ToString());
    }

    private static void ClickSubmitButton(IWebDriver driver)
    {
        // Dismiss cookie consent banners that may block the submit button
        DismissOverlays(driver);

        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
            var submitBtn = wait.Until(d =>
                d.FindElement(By.CssSelector("#tippabgabeForm button[type='submit'], #tippabgabeForm input[type='submit'], .formsubmit button")));

            try
            {
                submitBtn.Click();
            }
            catch (ElementClickInterceptedException)
            {
                // Fallback: click via JavaScript if an overlay is still blocking
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].click();", submitBtn);
            }
        }
        catch (WebDriverTimeoutException)
        {
            // No submit button found – kicktipp may auto-save on blur/change
        }
    }

    private static void DismissOverlays(IWebDriver driver)
    {
        // Try to close Sourcepoint CMP consent banner
        try
        {
            var acceptBtn = driver.FindElement(By.CssSelector(
                ".sp_choice_type_11, button[title='ALLES AKZEPTIEREN'], button[title='Accept All'], .sp-message button[aria-label^='Accept']"));
            acceptBtn.Click();
        }
        catch (NoSuchElementException)
        {
        }

        // Try to close any other overlays
        try
        {
            var closeBtn = driver.FindElement(By.CssSelector(
                "#sp_message_container_1230027 .sp_choice_type_11, button.sp_choice_type_11"));
            closeBtn.Click();
        }
        catch (NoSuchElementException)
        {
        }
    }

    private string GetBaseUrl() =>
        _configuration["Kicktipp:BaseUrl"] ?? "https://www.kicktipp.de";

    private string GetGroupName() =>
        _configuration["Kicktipp:GroupName"]
            ?? throw new InvalidOperationException("Kicktipp:GroupName is not configured");
}
