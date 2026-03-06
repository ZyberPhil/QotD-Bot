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

## Schritt 2: Self-hosted Runner auf dem Server einrichten

Damit GitHub Befehle auf deinem Server ausführen kann, ohne SSH zu nutzen, installieren wir den GitHub Runner:

1. Gehe in deinem GitHub-Repository zu **Settings -> Actions -> Runners**.
2. Klicke auf **New self-hosted runner**.
3. Wähle **Linux** und die passende Architektur (meist x64).
4. Folge exakt den Befehlen unter **Download** und **Configure** in deinem Terminal auf dem Server.
5. **Wichtig:** Wenn du gefragt wirst, welche Labels du vergeben willst, drücke einfach Enter (Standard-Labels reichen).
6. Starte den Runner am Ende als Service, damit er immer läuft:

   ```bash
   sudo ./svc.sh install
   sudo ./svc.sh start
   ```

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

- `/config-qotd channel #mein-kanal`
- `/config-qotd time 09:00`
- `/add-question date:2026-03-07 text:Deine Nachricht?`
