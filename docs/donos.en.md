[🇧🇷 Português](donos.md) · **🇺🇸 English** · [🇯🇵 日本語](donos.ja.md) · [🇩🇪 Deutsch](donos.de.md) · [🇫🇷 Français](donos.fr.md)

# 🏠 Channel owner guide

For whoever **sets up the room**: configure Discord, run the music bot and give your friends the connection details. You do this **once**; afterwards just keep the bot running when the group wants music.

> 🔐 **Golden rule:** the **Client Secret**, the **bot token** and the **webhook link** are like passwords. Send them only in private messages to your group. **Never** post them publicly or on GitHub.

## Part 1: configure Discord

### 1.1 Create the application
1. Open <https://discord.com/developers/applications> and click **New Application**. Name it (e.g. *FFBL*).
2. In **General Information** copy the **Application ID** (this is the **Client ID**).
3. In **OAuth2** click **Reset Secret** and copy the **Client Secret** (shown only once).
4. In **OAuth2 → Redirects** add `http://localhost` and save.

### 1.2 Create the bot
1. In the **Bot** menu click **Reset Token** and copy the **token** (keep it safe).
2. Further down, enable **Message Content Intent** and save.

### 1.3 Invite the bot to your server
1. In **OAuth2 → URL Generator** tick the scopes `bot` and `applications.commands`.
2. Tick permissions: *View Channels*, *Send Messages*, *Embed Links*, *Attach Files*, *Read Message History*, *Connect*, *Speak*.
3. Open the generated link and choose your server.

### 1.4 Rooms and webhook
1. In Discord: **Settings → Advanced → Developer Mode** (turn on).
2. Create (or pick) a **text room** for commands. Right-click it → **Copy Channel ID**. Keep it.
3. In that text room: **Edit Channel → Integrations → Webhooks → New Webhook → Copy Webhook URL**. This is the **webhook link**.
4. Create a **voice room** where the music will play.

### 1.5 Testers (OMNE only)
Only if you use proximity voice: in the portal go to **App Testers** and invite each friend (maximum **50**). Each must **accept the invite**.

## Part 2: run the music bot

The bot is a program that **runs on your computer**. **If it is closed, no music plays.** Keep it open while the group is using it.

1. Install **Python 3.11 or newer** (<https://www.python.org/downloads/>). During setup tick **Add Python to PATH**.
2. Download the **`bot`** folder from this repository (green **Code → Download ZIP** button, then extract).
3. Double-click **`INSTALAR.bat`** (first time only; it downloads what the bot needs).
4. Double-click **`INICIAR.bat`**.
5. The first time it asks for the **bot token** and the **room ID**. Paste each and press Enter. They are saved.
6. Join the voice room and type `/tocar` (or `!tocar song name` in the voice room's chat). The bot joins your room by itself. Use `/teste` to hear a beep and check the sound.

> `token.txt` and `config.json` stay **only on your computer** and are already blocked by `.gitignore`.

## Part 3: give the details to your group

Send each player **privately**:

1. **Client ID**
2. **Client Secret**
3. **Webhook link**
4. A reminder to **accept the tester invite** (if using OMNE)

Then point them to the [Player guide](usuarios.en.md).

## Common problems

| Symptom | What to do |
|---|---|
| Bot closes right away | Run `INSTALAR.bat` again and send a screenshot of the error. |
| "Invalid token" | Generate a new token (**Bot → Reset Token**) and delete `token.txt`. |
| Bot ignores commands | Check **Message Content Intent** and the room ID. |
| No music | Check that you are in the voice room and the bot is running. |

Questions? Open an **Issue** with a screenshot (hide the token and webhook!).
