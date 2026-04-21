# QotD Bot

Ein modularer Discord-Bot auf Basis von .NET 9, DSharpPlus v5 und PostgreSQL.
Der Bot bietet weit mehr als Question of the Day und ist in mehrere Features aufgeteilt, die pro Server konfigurierbar sind.

## Inhalt

1. Projektstatus
2. Tech Stack
3. Features im Ueberblick
4. Quick Start mit Docker
5. Lokale Entwicklung ohne Docker
6. Konfiguration
7. Befehlsreferenz
8. Architektur und Projektstruktur
9. API Endpoints
10. Deployment und Betrieb
11. Datenbank und Migrationen
12. Troubleshooting

## Projektstatus

- Ziel: Multi-Feature Community Bot mit sauberer Modularchitektur
- Laufzeit: .NET 9
- Datenhaltung: PostgreSQL (EF Core)
- Aktueller Stand Minigames:
  - Sessions fuer Blackjack und Tower sind server-spezifisch
  - Inaktive Spiele werden nach 5 Minuten automatisch beendet
  - Cleanup-Intervall laeuft alle 1 Minute

## Tech Stack

- .NET 9 (ASP.NET Core Host + Background Services)
- DSharpPlus 5 nightly (Commands, Interactivity, Event Handling)
- Entity Framework Core 9 + Npgsql
- Serilog (Console + File)
- SkiaSharp/Svg.Skia fuer Blackjack-Rendering
- Docker + Docker Compose fuer Deployment

## Features im Ueberblick

### QotD

- Geplante Tagesfragen pro Server
- Konfigurierbare Zielchannel, Zeit, Ping-Rolle, Posting-Template
- Manuelle Test-Posts

### Minigames

- Counting Channel
- Word Chain
- Blackjack
- Tower
- Session-/Lock-Scoping pro Server fuer Blackjack und Tower
- Auto-Cleanup inaktiver Sessions nach 5 Minuten

### Leveling

- XP/Level pro Server
- Rank und Leaderboard
- Konfigurierbare Level-Up Benachrichtigungen
- Voice-XP Konfiguration

### Link Moderation

- Whitelist/Blacklist Modi
- Regelverwaltung, Bypass-Rollen, Bypass-Channels
- Logchannel und Warnverhalten

### Teams

- Teamstatistiken und Rankings
- Mindestaktivitaet, Reports, Warnings, Role-History
- Leave-Tracking und Verlauf
- Teamsetup fuer dynamische Teamlisten

### Self Roles

- Self-Role Panel
- Gruppen, Optionen, Publish/Status
- Moderierte oder direkte Vergabe

### Birthdays

- User Geburtstage setzen/entfernen
- Serverweite Birthday-Konfiguration

### Temp Voice

- Trigger-Channel erzeugt temporaere Voice-Channels
- Rename, Limit, Lock/Unlock

### Logging und General

- Logrouting-Konfiguration
- Hilfemenue und Investigate Command
- Stats API

## Quick Start mit Docker

### Voraussetzungen

- Docker und Docker Compose
- Discord Bot Token

### 1) Repository klonen

```bash
git clone https://github.com/YOUR_USER/QotD-Bot.git
cd QotD-Bot
```

### 2) Umgebungsdatei anlegen

```bash
cp .env.example .env
```

Beispielwerte in .env:

```env
DISCORD_TOKEN=your_bot_token_here
DISCORD_GUILD_ID=123456789012345678
DISCORD_CHANNEL_ID=123456789012345678
POST_TIME=07:00
TIMEZONE=Europe/Berlin
POSTGRES_PASSWORD=change_me_to_a_strong_password
```

### 3) Starten

```bash
docker compose up -d
docker compose logs -f qotd-bot
```

Hinweis:
- Datenbankmigrationen werden beim Start automatisch ausgefuehrt.

## Lokale Entwicklung ohne Docker

### Voraussetzungen

- .NET 9 SDK
- PostgreSQL Instanz

### Start

```bash
dotnet restore src/QotD.Bot/QotD.Bot.csproj
dotnet build src/QotD.Bot/QotD.Bot.csproj
dotnet run --project src/QotD.Bot/QotD.Bot.csproj
```

### Nuetzliche Befehle

```bash
dotnet ef migrations add <MigrationName> --project src/QotD.Bot --output-dir Data/Migrations
dotnet ef database update --project src/QotD.Bot
```

## Konfiguration

Der Bot liest Konfiguration aus:

1. appsettings.json
2. appsettings.{Environment}.json
3. Environment Variables

Wichtige Keys:

| Key | Environment Variable | Beschreibung |
|---|---|---|
| Discord:Token | Discord__Token | Discord Bot Token |
| Discord:GuildId | Discord__GuildId | Default Guild ID (Bootstrap) |
| Discord:ChannelId | Discord__ChannelId | Default Channel ID (Bootstrap) |
| Scheduling:PostTime | Scheduling__PostTime | Standard Postzeit HH:mm |
| Scheduling:Timezone | Scheduling__Timezone | IANA Timezone |
| ConnectionStrings:Postgres | ConnectionStrings__Postgres | PostgreSQL Verbindungsstring |

Wichtig:
- Server-spezifische Laufzeitkonfiguration wird ueber Commands in der Datenbank gespeichert.
- Die Discord GuildId/ChannelId in der globalen Config dienen als Default/Bootstrap und nicht als hartes Multi-Server-Limit.

## Befehlsreferenz

Die wichtigsten Commands nach Feature:

### QotD

- qotd list
- qotd add
- qotd edit
- qotd delete
- qotd config channel
- qotd config time
- qotd config role
- qotd config template
- qotd config show
- qotd config reset
- qotd config test

### Minigames

- counting setup
- counting reset
- wordchain setup
- wordchain reset
- blackjack
- tower

### Leveling

- rank
- leaderboard
- levelingsetup setchannel
- levelingsetup disablenotifications
- levelingsetup voiceconfig
- levelingsetup setbanner
- levelingsetup clearbanner

### Link Moderation

- linkfilter status
- linkfilter enable
- linkfilter disable
- linkfilter mode
- linkfilter logchannel
- linkfilter dmwarn
- linkfilter channelwarn
- linkfilter ruleadd
- linkfilter ruleremove
- linkfilter rules
- linkfilter bypassroleadd
- linkfilter bypassroleremove
- linkfilter bypassroles
- linkfilter bypasschanneladd
- linkfilter bypasschannelremove
- linkfilter bypasschannels

### Teams

- team me
- team ranking
- team minima
- team reportsetup
- team reportdisable
- team warnings
- team rolehistory
- team warningsadd
- team warningsremove
- team leavestart
- team leaveend
- team leavestats
- team leavehistory
- team warningsnote lead
- team warningsnote statement
- team warningsnote resolve
- team warningsnote list
- teamsetup

### Self Roles

- selfrolesetup status
- selfrolesetup panel
- selfrolesetup group
- selfrolesetup optionadd
- selfrolesetup optionremove
- selfrolesetup publish

### Birthdays

- birthday set
- birthday remove
- birthdaysetup

### Temp Voice

- voice setup
- voice rename
- voice limit
- voice lock
- voice unlock

### General

- help
- investigate
- logsetup

## Architektur und Projektstruktur

Der Bot folgt einer modularen Architektur ueber IBotModule. Module werden zentral in Program.cs registriert.

Aktive Module:

- GeneralModule
- LevelingModule
- QotDModule
- TempVoiceModule
- MiniGamesModule
- LoggingModule
- TeamsModule
- SelfRolesModule
- BirthdaysModule
- LinkModerationModule

Projektstruktur:

```text
QotD-Bot/
├── src/QotD.Bot/
│   ├── Core/
│   ├── Configuration/
│   ├── Data/
│   ├── Features/
│   │   ├── Birthdays/
│   │   ├── Economy/
│   │   ├── General/
│   │   ├── Leveling/
│   │   ├── LinkModeration/
│   │   ├── Logging/
│   │   ├── MiniGames/
│   │   ├── QotD/
│   │   ├── SelfRoles/
│   │   ├── Teams/
│   │   └── TempVoice/
│   ├── Services/
│   ├── UI/
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Production.json
├── docker-compose.yml
├── Dockerfile
├── SERVER_SETUP.md
└── .env.example
```

## API Endpoints

### GET /api/stats

Liefert aggregierte Bot-Statistiken (mit kurzem Cache):

- totalQuestions
- totalGuilds
- totalAnswers
- activeMiniGames

## Deployment und Betrieb

### Docker Compose

Enthaltene Services:

- qotd-bot
- postgres

### Self-hosted Runner Deployment

Eine ausfuehrliche Schritt-fuer-Schritt-Anleitung liegt in SERVER_SETUP.md.

Kurzfassung:

1. Runner auf dem Zielserver installieren
2. GitHub Secrets setzen
3. Push auf main startet Build/Deploy Workflow

## Datenbank und Migrationen

- Hauptkontext: AppDbContext
- Separater Kontext fuer Leveling: LevelDatabaseContext
- Beide Kontexte werden beim Start migriert

Wichtig fuer EF CLI:

- Bei Migrationen fuer den Bot-Kontext immer den richtigen Kontext verwenden, wenn noetig explizit mit --context AppDbContext.

## Troubleshooting

### Bot startet nicht

- Pruefe DISCORD_TOKEN und ConnectionStrings
- Pruefe Container Logs:

```bash
docker compose logs -f qotd-bot
```

### Commands erscheinen nicht

- Bot-Rechte und OAuth2 Scopes pruefen
- Sicherstellen, dass Bot im Server ist
- Nach Neustart kurz auf Command-Sync warten

### Minigame Session wirkt weg

- Sessions werden nach 5 Minuten Inaktivitaet automatisch geschlossen
- Das ist erwartetes Verhalten

## Lizenz

MIT. Details in LICENSE.
