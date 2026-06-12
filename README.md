# Kicktipp MAF Workflow

Automated football match tipping agent using Selenium and LLM-based predictions.

## Architecture

```
┌─────────────────────┐     ┌──────────────────────┐     ┌──────────────────────┐
│  MatchFetchingWorker │────▶│  IMatchProvider      │────▶│  ITipProvider         │
│  (BackgroundService) │     │  (keyed singleton)   │     │  (singleton)          │
└──────────┬──────────┘     └──────────────────────┘     └──────────────────────┘
           │                                                        │
           │                 ┌──────────────────────┐               │
           └────────────────▶│  ITipSubmitter       │               │
                             │  (keyed singleton)   │               │
                             └──────────────────────┘               │
                                      │                             │
                                      ▼                             ▼
                             https://kicktipp.de              OpenAI API
                             (Selenium headless)            (YAML response)
```

| Component | Interface | Description |
|---|---|---|
| `KicktippMatchProvider` | `IMatchProvider` (keyed: `kicktipp`) | Scrapes kicktipp.de via Selenium Chrome headless. Logs in, navigates to the tipping page, parses the match table. |
| `OpenAiTipProvider` | `ITipProvider` | Sends match info to an OpenAI-compatible LLM. Expects a YAML response with `homeGoals`, `awayGoals`, `reasoning`. |
| `KicktippTipSubmitter` | `ITipSubmitter` (keyed: `kicktipp`) | Fills in tip values via Selenium and submits the tipping form. |
| `MatchFetchingWorker` | `BackgroundService` | Cron-based loop: fetch untipped upcoming matches, get LLM tip for each, submit tips. |

### Scheduling

The worker runs on a cron schedule (default: hourly at `0 * * * *`). It uses `Cronos` to parse the expression and `Task.Delay` to wait between runs.

### Providers

`IMatchProvider` and `ITipSubmitter` are **keyed services**. The active provider is selected via config:

```ini
Provider__Match=kicktipp
Provider__TipSubmitter=kicktipp
```

To add a new portal, implement the interface and register it with `AddKeyedSingleton<Interface, Impl>("mykey")` in `Program.cs`.

### Domain

- `Team(string Name)` – a football team
- `Match(Team First, Team Second, DateTimeOffset KickoffTime)` – a fixture
- `Tip(Match Match, int HomeGoals, int AwayGoals, string? Reasoning)` – a prediction

## Project Structure

```
src/KicktippAgent.Worker/
├── Interfaces/           # Abstractions (IMatchProvider, ITipProvider, ITipSubmitter)
├── Domain/               # Domain models (Team, Match, Tip)
│   └── Models.cs
├── Configuration/        # Options classes + .env loader
│   ├── Options.cs
│   └── DotEnvConfigurationExtensions.cs
├── Infrastructure/       # Concrete implementations (Selenium, OpenAI)
│   ├── KicktippMatchProvider.cs
│   ├── OpenAiTipProvider.cs
│   └── KicktippTipSubmitter.cs
├── MatchFetchingWorker.cs  # Cron-based background worker
├── Program.cs              # Host setup, DI registration
├── .env                    # Local configuration (gitignored)
└── Dockerfile
```

## Configuration

Configuration is loaded from a `.env` file (local dev) or environment variables (Docker). Section hierarchy uses `__` (double underscore) as separator.

Create `src/KicktippAgent.Worker/.env`:

```ini
# Kicktipp credentials
Kicktipp__Email=your-email@example.com
Kicktipp__Password=your-password
Kicktipp__GroupName=your-tipping-group
Kicktipp__BaseUrl=https://www.kicktipp.de

# OpenAI
OpenAI__ApiKey=sk-...
OpenAI__Model=gpt-4o
OpenAI__Preprompt=

# Schedule
Schedule__Cron=0 * * * *
Schedule__UpcomingWindow=24:00

# Provider selection
Provider__Match=kicktipp
Provider__TipSubmitter=kicktipp
```

In Docker, pass these as environment variables:

```bash
docker run --rm \
  -e Kicktipp__Email=... \
  -e Kicktipp__Password=... \
  -e OpenAI__ApiKey=... \
  kicktipp-worker
```

| Setting | Required | Default | Description |
|---|---|---|---|
| `Kicktipp:Email` | Yes | – | kicktipp.de account email |
| `Kicktipp:Password` | Yes | – | kicktipp.de account password |
| `Kicktipp:GroupName` | Yes | – | Tipping group URL slug (e.g. `my-group`) |
| `Kicktipp:BaseUrl` | No | `https://www.kicktipp.de` | Portal base URL |
| `OpenAI:ApiKey` | Yes | – | OpenAI API key |
| `OpenAI:Model` | No | `gpt-4o` | OpenAI model name |
| `OpenAI:Preprompt` | No | built-in default | Custom system prompt for the LLM |
| `Schedule:Cron` | No | `0 * * * *` | Cron expression (when to run) |
| `Schedule:UpcomingWindow` | No | `24:00` | Look-ahead window in `hh:mm` (e.g. `48:00` = 2 days) |
| `Provider:Match` | No | `kicktipp` | Key of the `IMatchProvider` to use |
| `Provider:TipSubmitter` | No | `kicktipp` | Key of the `ITipSubmitter` to use |

If `OpenAI:Preprompt` is empty, a built-in default is used (tailored for World Cup 2026).

## Run

```bash
dotnet run --project src/KicktippAgent.Worker/
```

## Docker

```bash
docker build -f src/KicktippAgent.Worker/Dockerfile -t kicktipp-worker .
docker run --rm kicktipp-worker
```

The Docker image includes Chromium for headless Selenium.
