# 🤖 QotD Bot — Question of the Day Discord Bot

A Discord bot that automatically posts a **Question of the Day** to a configured channel at a scheduled time, built with **.NET 9**, **DSharpPlus v5**, **PostgreSQL**, and **Docker**.

---

## ✨ Features

- 📅 **Per-Server Scheduling** — each server can configure its own channel and post time
- 💾 **PostgreSQL persistence** — questions and server settings are stored in a database
- 🔧 **Admin slash commands** — unified `/qotd` command group for configuration and management
- 🧱 **Modular Architecture** — Easily add new features (like TempVoice) by implementing `IBotModule`
- 🐳 **Docker-first** — full `docker-compose.yml` setup, multi-stage build
- 🚀 **Modern CI/CD** — SSH-free deployment via self-hosted GitHub runner

---

## 🚀 Quick Start (Local)

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- A [Discord Bot Token](https://discord.com/developers/applications)

### 1. Clone & configure

```bash
git clone https://github.com/YOUR_USER/QotD-Bot.git
cd QotD-Bot
cp .env.example .env
# Edit .env with your values
```

### 2. Fill in `.env`

```env
DISCORD_TOKEN=your_bot_token_here
DISCORD_GUILD_ID=123456789012345678    # Your server ID
DISCORD_CHANNEL_ID=123456789012345678  # Target channel ID
POST_TIME=07:00                        # 24-hour HH:mm in configured timezone
TIMEZONE=Europe/Berlin
POSTGRES_PASSWORD=change_me
```

### 3. Run

```bash
docker compose up -d
docker compose logs -f qotd-bot
```

The bot will auto-apply database migrations on startup.

---

## 🗄️ Database Schema

| Column | Type | Description |
|---|---|---|
| `Id` | `int` | Auto-increment primary key |
| `QuestionText` | `varchar(2000)` | The question content |
| `ScheduledFor` | `date` | Date to post (unique constraint) |
| `Posted` | `bool` | Whether the question has been sent |
| `CreatedAt` | `timestamptz` | Creation timestamp |

---

## 🔧 Slash Commands

| Command | Description | Permission |
|---|---|---|
| `/qotd config channel` | Set the channel for daily questions | Manage Server |
| `/qotd config time` | Set the daily post time (HH:mm) | Manage Server |
| `/qotd add date text` | Schedule a new question for a specific date | Manage Server |
| `/qotd list` | List upcoming unposted questions | Manage Server |
| `/qotd config test` | Trigger a test post and thread creation | Manage Server |
| `/investigate user` | Start an analysis of the specified subject | — |
| `/help` | Display the botanical guide/help menu | — |

### Example

```
/qotd add date:2026-03-07 text:What is your favourite programming language?
```

---

## 🏗️ Project Structure

```
QotD-Bot/
├── src/QotD.Bot/
│   ├── Core/              # Module infrastructure (IBotModule)
│   ├── Features/          # Feature-based isolation
│   │   ├── QotD/          # QotD Logic (Commands, Services)
│   │   ├── General/       # Shared commands (Help, Investigate)
│   │   └── TempVoice/     # Skeleton for future features
│   ├── Configuration/     # Strongly-typed settings POCOs
│   ├── Data/              # EF Core DbContext + shared models
│   ├── Services/          # Core bot connectivity services
│   ├── UI/                # Shared UI templates & design system
│   ├── Program.cs         # Modular bot initialization
│   ├── appsettings.json
│   └── appsettings.Production.json
├── Dockerfile
├── docker-compose.yml
├── NuGet.Config           # Adds DSharpPlus nightly feed
└── .github/workflows/
    └── deploy.yml         # CI/CD pipeline
```

---

## ⚙️ Configuration Reference

| Key | Environment Variable | Default | Description |
|---|---|---|---|
| `Discord:Token` | `Discord__Token` | — | Bot token (**required**) |
| `Discord:GuildId` | `Discord__GuildId` | — | Target guild ID (**required**) |
| `Discord:ChannelId` | `Discord__ChannelId` | — | Target channel ID (**required**) |
| `Scheduling:PostTime` | `Scheduling__PostTime` | `07:00` | Daily post time (HH:mm) |
| `Scheduling:Timezone` | `Scheduling__Timezone` | `Europe/Berlin` | IANA timezone |
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` | — | Postgres connection string |

---

## 🚀 CI/CD — GitHub Actions

The workflow (`.github/workflows/deploy.yml`) triggers on push to `main`:

1. Builds & pushes the Docker image to **GitHub Container Registry** (ghcr.io)
2. Triggers the **self-hosted runner** on your server to update the container (no SSH required!)

### Required GitHub Secrets

| Secret | Description |
|---|---|
| `DISCORD_TOKEN` | Bot token |
| `POSTGRES_PASSWORD` | Strong database password |
| `DISCORD_GUILD_ID` | (Optional) Default Guild ID |
| `DISCORD_CHANNEL_ID` | (Optional) Default Channel ID |
| `POST_TIME` | (Optional) Default post time |

### Server Setup (one-time)

```bash
# On your Ubuntu 24.04 server:
sudo mkdir -p /opt/qotd-bot
sudo chown $USER:$USER /opt/qotd-bot
# Copy docker-compose.yml to /opt/qotd-bot/
```

---

### 🧩 Adding New Features

The bot uses a modular architecture. To add a new feature (e.g., `TempVoice`):

1. Create a folder `src/QotD.Bot/Features/NewFeature`.
2. Implement the `IBotModule` interface:
   ```csharp
   public class NewFeatureModule : IBotModule {
       public void ConfigureServices(IServiceCollection services, IConfiguration config) { ... }
       public void ConfigureCommands(CommandsExtension commands) { ... }
   }
   ```
3. Add `new NewFeatureModule()` to the `modules` array in `Program.cs`.

---

### Running locally without Docker

1. Install [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. Start a local Postgres instance
3. Set environment variables or edit `appsettings.json`
4. Create the initial migration (first-time only):

   ```bash
   cd src/QotD.Bot
   dotnet ef migrations add InitialCreate --output-dir Data/Migrations
   ```

5. Run:

   ```bash
   dotnet run --project src/QotD.Bot
   ```

### Adding EF Core migrations

```bash
dotnet ef migrations add <MigrationName> \
  --project src/QotD.Bot \
  --output-dir Data/Migrations
```

---

## 📝 Discord Developer Portal Setup

1. Go to [discord.com/developers](https://discord.com/developers/applications)
2. Create an application → **Bot** tab → Enable:
   - ✅ **Server Members Intent** (if needed)
   - *Note: "Message Content Intent" is NOT required for slash commands*
3. Under **OAuth2 → URL Generator**:
   - Scopes: `bot`, `applications.commands`
   - Permissions: `Send Messages`, `Embed Links`
4. Copy the invite URL and add the bot to your server.

---

## 📄 License

MIT — see [LICENSE](LICENSE).
