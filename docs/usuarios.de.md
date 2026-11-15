[🇧🇷 Português](usuarios.md) · [🇺🇸 English](usuarios.en.md) · [🇯🇵 日本語](usuarios.ja.md) · **🇩🇪 Deutsch** · [🇫🇷 Français](usuarios.fr.md)

# 🎧 Spieleranleitung

Für alle, die FFBL im Spiel **nutzen**. Wer den Raum in Discord aufbaut, liest die [Anleitung für Kanalbesitzer](donos.de.md).

## Was du brauchst

- Final Fantasy XIV mit **XIVLauncher/Dalamud**.
- Die **Discord-App** auf demselben PC (nicht nur im Browser).
- Drei Angaben vom **Kanalbesitzer** (privat): **Client ID**, **Client Secret**, **Webhook-Link** (wie ein Passwort behandeln!).
- Für die **Leinwand** (optional): Plugin **Penumbra** und Minion **Plush Cushion**.

> 🎧 **Du willst nur zuhören?** Nichts installieren: einfach dem Discord-Sprachkanal beitreten.

## Schritt 1: Tester-Einladung annehmen
Der Besitzer fügt dich als **App-Tester** hinzu. Discord schickt dir eine Einladung: **Annehmen**. Ohne sie lässt Discord keine Verbindung zu.

## Schritt 2: Plugin installieren
1. Im Spiel `/xlsettings` eingeben.
2. Zu **Experimental** wechseln.
3. Bei **Benutzerdefinierte Plugin-Repositories** die URL einfügen, **+** klicken, dann **Speichern**:

```
https://raw.githubusercontent.com/yurammaruy/FFBL/main/repo.json
```

4. `/xlplugins` eingeben, **FFBL** unter *Alle Plugins* suchen und **Installieren** klicken.

## Schritt 3: Verbinden
1. `/ffbl` eingeben, der iPod öffnet sich.
2. Auf dem Verbindungsbildschirm **Client ID**, **Client Secret** und **Webhook** eintragen.
3. **Verbinden** klicken.
4. Im **Discord**-Fenster auf **Autorisieren** klicken.
5. Bei *Verbunden als (dein Name)* am Rad **MENU** drücken, um die Playlist zu öffnen.

## Bedienung

| Rad-Taste | Funktion |
|---|---|
| **MENU** | Wechselt zwischen Verbindung und Playlist |
| ⏮ | Vorheriges Lied |
| ⏭ | Überspringen |
| ⏯ | Abspielen / Pause |
| **Mitteltaste** | Öffnet das Wunschfeld (Name oder YouTube-Link) |
| **+ / −** | Lautstärke |
| Symbol in der unteren Ecke | Schließt das Fenster (wie `/ffbl`) |

Die Sprache des Plugins änderst du im Reiter **Idioma**.

## Leinwand (optional)
1. **Penumbra** installieren und aktiviert lassen.
2. Im Reiter **Projeção** die Schaltfläche zum Erstellen der Leinwand klicken. FFBL beschwört deinen *Plush Cushion* und macht ihn zur Leinwand.
3. Wähle: Standardbild, Cover oder ein Bild vom PC.
4. Größe, Position und Drehung mit **−** und **+** einstellen.

Wer FFBL nicht hat, sieht weiter den normalen Minion.

## OMNE (Näheschat): im Test
Im Reiter **OMNE**, **standardmäßig aus**. Erklärung auf der [Startseite](../README.de.md). Nur aktivieren, wenn die Gruppe gemeinsam testet.

## Häufige Probleme

| Symptom | Lösung |
|---|---|
| Hängt bei „Discord wird gesucht“ | Discord-App (nicht nur Browser) öffnen und erneut versuchen. |
| Keine Autorisierungsabfrage | Prüfen, ob die Tester-Einladung angenommen wurde. |
| Webhook-Link ungültig | Den ganzen Link einfügen (beginnt mit `https://discord.com/api/webhooks/`). |
| Leinwand erscheint nicht | Prüfen, ob Penumbra installiert und aktiviert ist. |
| Keine Musik | Den Kanalbesitzer bitten zu prüfen, ob der Bot läuft. |

Hilft nichts? Eröffne hier auf GitHub ein **Issue** mit Screenshot.
