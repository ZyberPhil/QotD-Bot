# 🚀 100% Tutorial: QotD Bot Server-Setup (Ohne SSH)

Dieses Tutorial zeigt dir, wie du den Bot über einen **Self-hosted GitHub Runner** direkt auf deinem Server installierst. Der Vorteil: Du musst keine SSH-Ports öffnen und keine SSH-Keys in GitHub hinterlegen.

## Voraussetzungen

- Ein Linux-Server (z.B. Ubuntu) mit installiertem **Docker** und **Docker Compose**.
- Ein GitHub-Repository für den Bot-Code.

---

## Schritt 1: Server-Vorbereitung

Erstelle das Verzeichnis für den Bot:

```bash
# Auf deinem Server ausführen:
sudo mkdir -p /opt/qotd-bot
sudo chown $USER:$USER /opt/qotd-bot
```

---

## Schritt 2: GitHub Runner auf dem Server einrichten

Damit GitHub Befehle auf deinem Server ausführen kann, ohne SSH zu nutzen, muss ein Runner auf dem Server laufen. Wähle eine der folgenden Optionen:

### Option A: Manueller Runner (Direkt auf dem Host)

*Empfohlen, wenn du den Runner als festen System-Dienst installieren willst.*

1. Gehe in deinem GitHub-Repository zu **Settings -> Actions -> Runners**.
2. Klicke auf **New self-hosted runner**.
3. Wähle **Linux** und die passende Architektur (meist x64).
4. Folge den Befehlen unter **Download** und **Configure**.
5. Installiere den Runner als Dienst:

   ```bash
   sudo ./svc.sh install
   sudo ./svc.sh start
   ```

### Option B: Docker-Runner (Empfohlen für mehrere Bots) 🐳

*Ideal, wenn du bereits einen Runner für einen anderen Bot hast. Du startest einfach einen zweiten Container.*

> [!IMPORTANT]
> Der **Registration Token** von GitHub ist nur für **ca. 1 Stunde** gültig. Wenn du die Fehlermeldung "Invalid configuration provided for token" erhältst, ist der Token wahrscheinlich abgelaufen. Hole dir in diesem Fall einen neuen Token aus den GitHub-Einstellungen.

1. Gehe in deinem GitHub-Repository zu **Settings -> Actions -> Runners -> New self-hosted runner**.
2. Kopiere dir nur den **Registration Token** (hinter dem Flag `--token`).
3. Starte den Runner-Container auf deinem Server (Beispiel mit `my-actions-runner` Image):

```bash
docker run -d --restart always --name github-runner-qotd \
  -e REPO_URL=https://github.com/DEIN_USER/QotD-Bot \
  -e RUNNER_NAME=qotd-bot-runner \
  -e RUNNER_TOKEN=DEIN_TOKEN_VON_GITHUB \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /opt/qotd-bot:/opt/qotd-bot \
  myoung34/github-runner:latest
```

*Hinweis: Das Image `myoung34/github-runner` hat Docker bereits vorinstalliert.*

---

## Schritt 3: Discord Developer Portal

1. Gehe zu [discord.com/developers](https://discord.com/developers/applications).
2. Erstelle eine neue App: **"QotD Bot"**.
3. Kopiere unter **Bot** den **Token**.
4. Aktiviere unter **OAuth2 -> URL Generator**:
   - Scopes: `bot`, `applications.commands`.
   - Permissions: `Send Messages`, `Embed Links`.
5. Lade den Bot auf deinen Server ein.

---

## Schritt 4: GitHub Secrets einrichten

Gehe zu **Settings -> Secrets and variables -> Actions** und erstelle diese **New repository secrets**:

| Secret Name | Beschreibung |
| :--- | :--- |
| `DISCORD_TOKEN` | Dein Bot-Token |
| `DISCORD_GUILD_ID` | Die ID deines Discord-Servers |
| `DISCORD_CHANNEL_ID` | Der Standard-Kanal (wird per Befehl überschrieben) |
| `POST_TIME` | Standard-Post-Zeit (z.B. `08:00`) |
| `POSTGRES_PASSWORD` | Ein sicheres Passwort für die Datenbank |

---

## Schritt 5: Deployment starten

Ich habe die Datei `.github/workflows/deploy.yml` bereits so angepasst, dass sie deinen `self-hosted` Runner nutzt. Du musst jetzt nur noch den Code pushen:

```bash
git add .
git commit -m "Setup für self-hosted Runner abgeschlossen"
git push origin main
```

**Was passiert jetzt?**

1. GitHub Actions baut das Docker-Image (mit .NET 9).
2. Der Runner auf deinem Server empfängt den Befehl.
3. Er erstellt lokal die `.env` Datei mit deinen Secrets.
4. Er führt `docker compose pull` und `up -d` direkt auf dem Server aus.

---

## Schritt 6: Erfolgskontrolle

Prüfe die Container auf deinem Server:

```bash
cd /opt/qotd-bot
docker compose ps
docker compose logs -f qotd-bot
```

### Befehle im Discord nutzen

- `/qotd config channel #mein-kanal`
- `/qotd config time 09:00`
- `/qotd add date:2026-03-07 text:Deine Nachricht?`
- `/help`
