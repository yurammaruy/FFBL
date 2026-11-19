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
2. Tick permissions: *View Channels*, *Send Messages*, *Embed Links*, *Attach Files*, *Read Message History*, *Manage Messages*, *Connect*, *Speak*.
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

1. Install **Python 3.11 or newer**. Open the Start menu, type `PowerShell`, open it and paste the command below. Then **close and reopen PowerShell** and check with `python --version`.

   ```
   winget install -e --id Python.Python.3.12
   ```
   If `winget` is missing on your Windows, install Python from the Microsoft Store (search "Python 3.12").
2. Download just the bot with one click: [**FFBL-bot.zip**](https://github.com/yurammaruy/FFBL/releases/latest/download/FFBL-bot.zip). Right-click the file → **Extract all**. (It is only a few files; no need to download the whole repository.)
3. Double-click **`INSTALAR.bat`** (first time only; it downloads what the bot needs).
4. Double-click **`INICIAR.bat`**. The **bot panel** opens.
5. In the panel, click **+ Nova sala** (new room). Fill in, in this order: **Nome do Canal** (channel name), **Nome da Sala** (room name), **Client ID**, **Client Secret**, **ID da SALA** (room ID), **Token do bot** and **Link do Webhook**. Click **Salvar** (save): the fields lock (use **Editar informacoes** to change them) and the **connection code** appears. Each room gets its own tab, and **only one is on at a time**.
6. Click **Iniciar bot** (start bot) on the room's tab. If you **minimize** the panel, it goes next to the clock and the bot keeps playing. The **Basico** tab shows what the bot is doing in plain sentences; **Detalhado** shows everything.
7. Join the voice room and type `/tocar` (or `!tocar song name` in the voice room's chat). The bot joins your room by itself. Use `/teste` to hear a beep and check the sound.

> `token.txt` and `config.json` stay **only on your computer** and are already blocked by `.gitignore`.

## Part 3: give the details to your group

In the bot panel, on the room's tab, use the **Copiar** button under the **connection code** and send it **privately** to each player:

1. The **connection code** (starts with `FFBL1-`). It already carries the Client ID, Client Secret, webhook link and the OMNE room, so nothing else needs to be sent.
2. A reminder to **accept the tester invite** (if using OMNE)

> The code works like a password: whoever has it can post in the room. If it leaks, delete the webhook in Discord and generate the code again. FFBL **forgets the code** whenever the character logs out, so players paste it again each session.

Then point them to the [Player guide](usuarios.en.md).

## Common problems

| Symptom | What to do |
|---|---|
| `invalid_request` / "Missing redirect_uri" when connecting | In the Discord portal, in the app being used: **OAuth2 → Redirects**, add `http://localhost` and click **Save Changes**. Also check the Client ID belongs to the right app. |
| Bot closes right away | Run `INSTALAR.bat` again and send a screenshot of the error. |
| "Invalid token" | Generate a new token (**Bot → Reset Token**) and delete `token.txt`. |
| Bot ignores commands | Check **Message Content Intent** and the room ID. |
| No music | Check that you are in the voice room and the bot is running. |

Questions? Open an **Issue** with a screenshot (hide the token and webhook!).
