# AGENTS.md

## Project overview

.NET 10 console worker that scrapes kicktipp.de for upcoming matches and uses an OpenAI-compatible LLM to generate football tips.

## Build & run

```bash
dotnet build src/KicktippAgent.Worker/
dotnet run --project src/KicktippAgent.Worker/
```

## Architecture rules

- All code in the single `KicktippAgent.Worker` project (no multi-project solution yet)
- **Folder structure**: `Interfaces/`, `Domain/`, `Configuration/`, `Infrastructure/` – root has `Program.cs` and `MatchFetchingWorker.cs`
- All domain types are in `Domain/Models.cs` under namespace `KicktippAgent.Worker`
- Log messages and comments **must be in English**
- Interfaces: `IMatchProvider`, `ITipProvider`, `ITipSubmitter` – follow the same pattern for new providers
- DI registration via `Program.cs` – use `AddKeyedSingleton<Interface, Impl>("key")` for swappable providers, `AddSingleton` for fixed ones, `AddHostedService` for workers
- Config keys use colon syntax: `Kicktipp:Email`, `OpenAI:ApiKey`, etc.
- All configuration sections must have a corresponding options class in `Configuration/Options.cs` with `[Required]` DataAnnotations
- Options are registered via `AddOptions<T>().Bind(section).ValidateDataAnnotations()` and injected as `IOptions<T>`

## Key learnings from this session

### Config loading
- `Host.CreateApplicationBuilder` does **not** auto-load `appsettings.json` in .NET 10
- Manual setup required: `.AddDotEnv(Path.Combine(AppContext.BaseDirectory, ".env"))`
- `builder.Environment.ContentRootPath` must also be set to `AppContext.BaseDirectory`
- `CopyToOutputDirectory` must be set to `PreserveNewest` in the `.csproj`
- Config is loaded from `.env` file (local) and environment variables (Docker)
- `.env` uses `__` as section separator (e.g. `Kicktipp__Email`) – converted to `:` for `IConfiguration`
- `.env` is in `.gitignore` to prevent credential leaks

### Kicktipp scraping
- Login form: `#loginFormular` → fields `kennung` (email) + `passwort` (password), POST to `/info/profil/loginaction`
- Tippabgabe page: `/<groupname>/tippabgabe`
- Match table: `#tippabgabeSpiele` (class `ktable`), rows: `tbody tr.datarow`
- Date column: `td.kicktipp-time` (format: `dd.MM.yy HH:mm`)
- Home team: `td.col1`, Away team: `td.col2`
- Tipp status: `td.col3` – class `nichttippbar` means deadline passed, `kicktipp-tippabgabe` means tippable
- Already tipped: `input[type='text']` in `td.col3` has a non-empty `value` attribute
- Spieltag navigation: `.spieltagsauswahl .dropdowncontent .menu` with links to other matchdays

### OpenAI integration
- Use official `OpenAI` NuGet package (v2.3.0+)
- YAML response format via `YamlDotNet` with `CamelCaseNamingConvention`
- Preprompt is configurable via `.env`; falls back to built-in default
- Structured output via `ChatResponseFormat.CreateJsonSchemaFormat` was tried but replaced with plain YAML text response

### Selenium headless
- Chrome options: `--headless=new`, `--no-sandbox`, `--disable-dev-shm-usage`, `--disable-gpu`
- Docker image needs Chromium installed: `apt-get install -y chromium`
- Selenium 4.x handles ChromeDriver auto-download, no manual driver management needed

### Tip submission
- Tipp inputs per row: `input[id$='_heimTipp']` and `input[id$='_gastTipp']` inside `td.col3`
- Submit button: `#tippabgabeForm button[type='submit']` or `.formsubmit button`
- Cookie consent overlay (`#sp_message_iframe_1230027`) can block the submit button – dismiss via `button.sp_choice_type_11` or fall back to JavaScript `arguments[0].click()`
- Tips are submitted per-row by matching team names from `td.col1`/`td.col2` against `Tip.Match`

### Scheduling
- Worker uses `Cronos` (v0.10.0) to parse cron expressions
- Config: `Schedule:Cron` (5-field cron, e.g. `0 * * * *` for hourly)
- Config: `Schedule:UpcomingWindow` in `hh:mm` format (e.g. `24:00` for 1 day)
- `UpcomingWindow` uses a custom parser since `TimeSpan.Parse` does not support hours > 23 in `hh:mm` format
- Worker runs in a continuous loop: waits for next cron trigger → executes full workflow → repeats

### Provider architecture
- `IMatchProvider` and `ITipSubmitter` are keyed services – resolved at runtime via `Provider:Match` / `Provider:TipSubmitter` config values
- New providers: implement the interface, register with `AddKeyedSingleton<Interface, Impl>("key")`, set the config key
- `ITipProvider` is not keyed (always OpenAI for now)
- `MatchFetchingWorker` resolves keyed services via `IServiceProvider.GetRequiredKeyedService<T>(key)`
