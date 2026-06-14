# Kicktipp Agent

Automated football tipping agent — scrapes [kicktipp.de](https://www.kicktipp.de) for upcoming matches, generates predictions via an OpenAI-compatible LLM, and submits the tips.

## Setup

Create `src/KicktippAgent.Worker/.env`:

```ini
# Kicktipp
Kicktipp__Email=your-email@example.com
Kicktipp__Password=your-password
Kicktipp__GroupName=your-tipping-group
Kicktipp__BaseUrl=https://www.kicktipp.de

# OpenAI
OpenAI__ApiKey=sk-...
OpenAI__Model=gpt-5.4-mini
OpenAI__Preprompt=

# Schedule
Schedule__Cron=0 * * * *
Schedule__UpcomingWindow=24:00

# Provider
Provider__Match=kicktipp
Provider__TipSubmitter=kicktipp

# Ntfy notifications (optional)
Ntfy__Enabled=true
Ntfy__Topic=kicktipp_agent
Ntfy__Server=https://ntfy.sh
Ntfy__AccessToken=
```

Configuration is loaded from the `.env` file (local dev) or environment variables (Docker). If `OpenAI:Preprompt` is empty, a built-in default is used. The agent can optionally send push notifications via [ntfy.sh](https://ntfy.sh) or a self-hosted Ntfy server — on startup, when tips are placed, and on failures.

| Setting | Required | Default | Description |
|---|---|---|---|
| `Kicktipp:Email` | Yes | – | kicktipp.de account email |
| `Kicktipp:Password` | Yes | – | kicktipp.de account password |
| `Kicktipp:GroupName` | Yes | – | Tipping group URL slug (e.g. `my-group`) |
| `Kicktipp:BaseUrl` | No | `https://www.kicktipp.de` | Portal base URL |
| `OpenAI:ApiKey` | Yes | – | OpenAI API key |
| `OpenAI:Model` | No | `gpt-5.4-mini` | OpenAI model name |
| `OpenAI:Preprompt` | No | built-in default | Custom system prompt for the LLM |
| `Schedule:Cron` | No | `0 * * * *` | Cron expression (when to run) |
| `Schedule:UpcomingWindow` | No | `24:00` | Look-ahead window in `hh:mm` (e.g. `48:00` = 2 days) |
| `Provider:Match` | No | `kicktipp` | Key of the `IMatchProvider` to use |
| `Provider:TipSubmitter` | No | `kicktipp` | Key of the `ITipSubmitter` to use |
| `Ntfy:Enabled` | No | `false` | Enable push notifications |
| `Ntfy:Topic` | No | `kicktipp_agent` | Ntfy topic to publish to |
| `Ntfy:Server` | No | `https://ntfy.sh` | Ntfy server URL |
| `Ntfy:AccessToken` | No | – | Access token for protected topics |

## Run

```bash
dotnet run --project src/KicktippAgent.Worker/
```

## Docker

Pre-built images are available on [GitHub Container Registry](https://github.com/berndver/kicktipp-agent/pkgs/container/kicktipp-agent):

```bash
docker pull ghcr.io/berndver/kicktipp-agent:latest
```

Run directly:

```bash
docker run --rm \
  -e Kicktipp__Email=... \
  -e Kicktipp__Password=... \
  -e Kicktipp__GroupName=... \
  -e OpenAI__ApiKey=... \
  ghcr.io/berndver/kicktipp-agent:latest
```

Or use Docker Compose. Create a `docker-compose.yml`:

```yaml
services:
  kicktipp-agent:
    image: ghcr.io/berndver/kicktipp-agent:latest
    environment:
      Kicktipp__Email: your-email@example.com
      Kicktipp__Password: your-password
      Kicktipp__GroupName: your-tipping-group
      Kicktipp__BaseUrl: https://www.kicktipp.de
      OpenAI__ApiKey: sk-...
      OpenAI__Model: gpt-5.4-mini
      OpenAI__Preprompt: ""
      Schedule__Cron: "0 * * * *"
      Schedule__UpcomingWindow: "24:00"
      Provider__Match: kicktipp
      Provider__TipSubmitter: kicktipp
    restart: unless-stopped
```

```bash
docker compose up -d
```

The image includes Chromium for headless Selenium.

To build the image yourself:

```bash
docker build -f src/KicktippAgent.Worker/Dockerfile -t kicktipp-agent .
```

## Contributing

Pull requests are welcome. This project is open source under the [MIT License](LICENSE).
