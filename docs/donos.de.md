[🇧🇷 Português](donos.md) · [🇺🇸 English](donos.en.md) · [🇯🇵 日本語](donos.ja.md) · **🇩🇪 Deutsch** · [🇫🇷 Français](donos.fr.md)

# 🏠 Anleitung für Kanalbesitzer

Für alle, die den **Raum aufbauen**: Discord einrichten, den Musikbot starten und den Freunden die Zugangsdaten geben. Das machst du **einmal**; danach muss nur der Bot laufen, wenn die Gruppe Musik möchte.

> 🔐 **Goldene Regel:** **Client Secret**, **Bot-Token** und **Webhook-Link** sind wie Passwörter. Nur privat an deine Gruppe senden. **Niemals** öffentlich oder auf GitHub posten.

## Teil 1: Discord einrichten

### 1.1 Anwendung erstellen
1. <https://discord.com/developers/applications> öffnen und **New Application** klicken. Namen vergeben (z. B. *FFBL*).
2. Unter **General Information** die **Application ID** kopieren (das ist die **Client ID**).
3. Unter **OAuth2** **Reset Secret** klicken und das **Client Secret** kopieren (wird nur einmal angezeigt).
4. Unter **OAuth2 → Redirects** `http://localhost` hinzufügen und speichern.

### 1.2 Bot erstellen
1. Im Menü **Bot** auf **Reset Token** klicken und den **Token** kopieren (sicher aufbewahren).
2. Weiter unten **Message Content Intent** aktivieren und speichern.

### 1.3 Bot auf den Server einladen
1. Unter **OAuth2 → URL Generator** die Scopes `bot` und `applications.commands` anhaken.
2. Berechtigungen: *View Channels*, *Send Messages*, *Embed Links*, *Attach Files*, *Read Message History*, *Manage Messages*, *Connect*, *Speak*.
3. Den erzeugten Link öffnen und deinen Server wählen.

### 1.4 Kanäle und Webhook
1. In Discord: **Einstellungen → Erweitert → Entwicklermodus** (einschalten).
2. Einen **Textkanal** für Befehle erstellen (oder wählen). Rechtsklick → **Kanal-ID kopieren**. Aufbewahren.
3. In diesem Textkanal: **Kanal bearbeiten → Integrationen → Webhooks → Neuer Webhook → Webhook-URL kopieren**. Das ist der **Webhook-Link**.
4. Einen **Sprachkanal** erstellen, in dem die Musik läuft.

### 1.5 Tester (nur für OMNE)
Nur bei Nähe-Sprachchat: im Portal **App Testers** öffnen und jeden Freund einladen (maximal **50**). Jeder muss die **Einladung annehmen**.

## Teil 2: Musikbot starten

Der Bot ist ein Programm, das **auf deinem Computer läuft**. **Ist es geschlossen, gibt es keine Musik.** Lass es offen, solange die Gruppe es nutzt.

1. Installiere **Python 3.11 oder neuer**. Startmenü öffnen, `PowerShell` eingeben, öffnen und den folgenden Befehl einfügen. Danach **PowerShell schließen und neu öffnen** und mit `python --version` prüfen.

   ```
   winget install -e --id Python.Python.3.12
   ```
   Fehlt `winget` unter deinem Windows, installiere Python über den Microsoft Store (nach „Python 3.12“ suchen).
2. Nur den Bot mit einem Klick herunterladen: [**FFBL-bot.zip**](https://github.com/yurammaruy/FFBL/releases/latest/download/FFBL-bot.zip). Rechtsklick auf die Datei → **Alle extrahieren**. (Es sind nur wenige Dateien; das ganze Repository ist nicht nötig.)
3. **`INSTALAR.bat`** doppelklicken (nur beim ersten Mal; lädt, was der Bot braucht).
4. **`INICIAR.bat`** doppelklicken. Das **Bot-Fenster** öffnet sich.
5. Im Fenster auf **+ Nova sala** (neuer Raum) klicken und der Reihe nach ausfüllen: **Nome do Canal** (Kanalname), **Nome da Sala** (Raumname), **Client ID**, **Client Secret**, **ID da SALA** (Raum-ID), **Token do bot** und **Link do Webhook**. Auf **Salvar** (speichern) klicken: Die Felder werden gesperrt (zum Ändern **Editar informacoes**) und der **Verbindungscode** erscheint. Jeder Raum hat einen eigenen Tab; **nur einer ist gleichzeitig aktiv**.
6. Im Tab des Raums auf **Iniciar bot** (Bot starten) klicken. Beim **Minimieren** wandert das Fenster neben die Uhr, der Bot spielt weiter. Der Tab **Basico** zeigt in einfachen Sätzen, was der Bot tut; **Detalhado** zeigt alles.
7. Den Sprachkanal betreten und `/tocar` eingeben eingeben (oder `!tocar Liedname` im Chat des Sprachkanals). Der Bot tritt selbst bei. Mit `/teste` hörst du einen Piepton und prüfst den Ton.

> `token.txt` und `config.json` bleiben **nur auf deinem Computer** und sind in `.gitignore` bereits gesperrt.

## Teil 3: Daten an die Gruppe weitergeben

Im Bot-Fenster, im Tab des Raums, den Knopf **Copiar** unter dem **Verbindungscode** benutzen und den Code **privat** an jeden Spieler senden:

1. Der **Verbindungscode** (beginnt mit `FFBL1-`). Er enthält schon Client ID, Client Secret, Webhook-Link und den OMNE-Raum; sonst muss nichts gesendet werden.
2. Den Hinweis, die **Tester-Einladung anzunehmen** (bei OMNE)

> Der Code wirkt wie ein Passwort: Wer ihn hat, kann in den Raum schreiben. Bei einem Leak den Webhook in Discord löschen und den Code neu erzeugen. FFBL **vergisst den Code**, sobald sich der Charakter abmeldet; Spieler fügen ihn jede Sitzung neu ein.

Dann verweise auf die [Spieleranleitung](usuarios.de.md).

## Häufige Probleme

| Symptom | Lösung |
|---|---|
| `invalid_request` / „Missing redirect_uri“ beim Verbinden | Im Discord-Portal, in der verwendeten App: **OAuth2 → Redirects**, `http://localhost` hinzufügen und **Save Changes** klicken. Prüfe auch, ob die Client ID zur richtigen App gehört. |
| Bot schließt sofort | `INSTALAR.bat` erneut ausführen und Screenshot des Fehlers schicken. |
| „Ungültiger Token“ | Neuen Token erzeugen (**Bot → Reset Token**) und `token.txt` löschen. |
| Bot ignoriert Befehle | **Message Content Intent** und Kanal-ID prüfen. |
| Keine Musik | Prüfen, ob du im Sprachkanal bist und der Bot läuft. |

Fragen? Eröffne ein **Issue** mit Screenshot (Token und Webhook verdecken!).
