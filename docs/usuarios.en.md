[🇧🇷 Português](usuarios.md) · **🇺🇸 English** · [🇯🇵 日本語](usuarios.ja.md) · [🇩🇪 Deutsch](usuarios.de.md) · [🇫🇷 Français](usuarios.fr.md)

# 🎧 Player guide

For people who **will use** FFBL in game. If you are setting up the room on Discord, read the [Channel owner guide](donos.en.md).

## What you need

- Final Fantasy XIV with **XIVLauncher/Dalamud**.
- The **Discord app** open on the same PC (not just the browser).
- Three items the **channel owner** will send you privately: **Client ID**, **Client Secret**, **webhook link** (treat it like a password!).
- For the **screen** (optional): the **Penumbra** plugin and the **Plush Cushion** minion.

> 🎧 **Just want to listen?** Nothing to install: join the Discord voice room.

## Step 1: accept the tester invite
The owner will add you as an **app tester**. Discord sends you an invite: **accept** it. Without it Discord will not let the plugin connect.

## Step 2: install the plugin
1. In game, type `/xlsettings`.
2. Go to **Experimental**.
3. Under **Custom Plugin Repositories**, paste the URL below, click **+**, then **Save**:

```
https://raw.githubusercontent.com/yurammaruy/FFBL/main/repo.json
```

4. Type `/xlplugins`, find **FFBL** in *All Plugins* and click **Install**.

## Step 3: connect
1. Type `/ffbl` to open the iPod.
2. On the connection screen fill in **Client ID**, **Client Secret** and **Webhook**.
3. Click **Connect**.
4. Look at the **Discord** window and click **Authorize**.
5. When it says *Connected as (your name)*, press **MENU** on the wheel to open the playlist.

## How to use

| Wheel button | What it does |
|---|---|
| **MENU** | Switches between connection screen and playlist |
| ⏮ | Previous song |
| ⏭ | Skip |
| ⏯ | Play / pause |
| **Center button** | Opens the request field (name or YouTube link) |
| **+ / −** | Volume |
| Icon at the bottom corner | Closes the window (same as `/ffbl`) |

Change the plugin language in the **Idioma** tab.

## Screen (optional)
1. Install **Penumbra** and keep it enabled.
2. In the **Projeção** tab click the create-screen button. FFBL summons your *Plush Cushion* and turns it into the screen.
3. Pick what to show: default image, song cover or an image from your PC.
4. Adjust size, position and rotation with **−** and **+**.

People without FFBL still see the normal minion.

## OMNE (proximity voice): testing
In the **OMNE** tab, **off by default**. Read the explanation on the [main page](../README.en.md). Enable it only if your group is testing together.

## Common problems

| Symptom | What to do |
|---|---|
| Stuck on "looking for Discord" | Open the Discord app (not only the browser) and try again. |
| No authorize prompt | Make sure you accepted the tester invite. |
| Webhook link invalid | Paste the whole link, starting with `https://discord.com/api/webhooks/`. |
| Screen doesn't show | Check that Penumbra is installed and enabled. |
| No music | Ask the channel owner to check that the bot is running. |

If nothing helps, open an **Issue** here on GitHub with a screenshot.
