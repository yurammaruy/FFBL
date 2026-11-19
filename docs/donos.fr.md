[🇧🇷 Português](donos.md) · [🇺🇸 English](donos.en.md) · [🇯🇵 日本語](donos.ja.md) · [🇩🇪 Deutsch](donos.de.md) · **🇫🇷 Français**

# 🏠 Guide du propriétaire du salon

Pour celui ou celle qui **monte le salon** : configurer Discord, lancer le bot musical et donner aux amis les informations de connexion. Vous le faites **une seule fois** ; ensuite il suffit de laisser le bot actif quand le groupe veut de la musique.

> 🔐 **Règle d'or :** le **Client Secret**, le **token du bot** et le **lien webhook** sont comme des mots de passe. Envoyez-les uniquement en privé à votre groupe. **Jamais** en public ni sur GitHub.

## Partie 1 : configurer Discord

### 1.1 Créer l'application
1. Ouvrez <https://discord.com/developers/applications> et cliquez sur **New Application**. Donnez un nom (ex. *FFBL*).
2. Dans **General Information**, copiez l'**Application ID** (c'est le **Client ID**).
3. Dans **OAuth2**, cliquez sur **Reset Secret** et copiez le **Client Secret** (affiché une seule fois).
4. Dans **OAuth2 → Redirects**, ajoutez `http://localhost` et enregistrez.

### 1.2 Créer le bot
1. Dans le menu **Bot**, cliquez sur **Reset Token** et copiez le **token** (gardez-le précieusement).
2. Plus bas, activez **Message Content Intent** et enregistrez.

### 1.3 Inviter le bot sur votre serveur
1. Dans **OAuth2 → URL Generator**, cochez les portées `bot` et `applications.commands`.
2. Cochez les permissions : *View Channels*, *Send Messages*, *Embed Links*, *Attach Files*, *Read Message History*, *Manage Messages*, *Connect*, *Speak*.
3. Ouvrez le lien généré et choisissez votre serveur.

### 1.4 Salons et webhook
1. Dans Discord : **Paramètres → Avancés → Mode développeur** (activez).
2. Créez (ou choisissez) un **salon textuel** pour les commandes. Clic droit → **Copier l'identifiant du salon**. Gardez-le.
3. Dans ce salon : **Modifier le salon → Intégrations → Webhooks → Nouveau webhook → Copier l'URL du webhook**. C'est le **lien webhook**.
4. Créez un **salon vocal** où la musique sera diffusée.

### 1.5 Testeurs (OMNE uniquement)
Seulement pour la voix de proximité : dans le portail, **App Testers**, invitez chaque ami (maximum **50**). Chacun doit **accepter l'invitation**.

## Partie 2 : lancer le bot musical

Le bot est un programme qui **tourne sur votre ordinateur**. **Fermé, il ne joue aucune musique.** Laissez-le ouvert tant que le groupe l'utilise.

1. Installez **Python 3.11 ou plus récent**. Ouvrez le menu Démarrer, tapez `PowerShell`, ouvrez-le et collez la commande ci-dessous. Puis **fermez et rouvrez PowerShell** et vérifiez avec `python --version`.

   ```
   winget install -e --id Python.Python.3.12
   ```
   Si `winget` est absent de votre Windows, installez Python depuis le Microsoft Store (cherchez « Python 3.12 »).
2. Téléchargez uniquement le bot en un clic : [**FFBL-bot.zip**](https://github.com/yurammaruy/FFBL/releases/latest/download/FFBL-bot.zip). Clic droit sur le fichier → **Extraire tout**. (Seulement quelques fichiers ; inutile de télécharger tout le dépôt.)
3. Double-cliquez sur **`INSTALAR.bat`** (première fois uniquement ; il télécharge ce dont le bot a besoin).
4. Double-cliquez sur **`INICIAR.bat`**. Le **panneau du bot** s'ouvre.
5. Dans le panneau, cliquez sur **+ Nova sala** (nouveau salon) et remplissez dans l'ordre : **Nome do Canal** (nom du canal), **Nome da Sala** (nom du salon), **Client ID**, **Client Secret**, **ID da SALA** (identifiant du salon), **Token do bot** et **Link do Webhook**. Cliquez sur **Salvar** (enregistrer) : les champs se verrouillent (pour modifier, **Editar informacoes**) et le **code de connexion** apparaît. Chaque salon a son onglet ; **un seul est actif à la fois**.
6. Cliquez sur **Iniciar bot** (démarrer le bot) dans l'onglet du salon. Si vous **réduisez** le panneau, il va près de l'horloge et le bot continue. L'onglet **Basico** montre en phrases simples ce que fait le bot ; **Detalhado** montre tout.
7. Rejoignez le salon vocal et tapez `/tocar` (ou `!tocar nom du morceau` dans le chat du salon vocal). Le bot vous rejoint tout seul. `/teste` joue un bip pour vérifier le son.

> `token.txt` et `config.json` restent **uniquement sur votre ordinateur** et sont déjà bloqués par `.gitignore`.

## Partie 3 : transmettre les infos au groupe

Dans le panneau du bot, dans l'onglet du salon, utilisez le bouton **Copiar** sous le **code de connexion** et envoyez-le **en privé** à chaque joueur :

1. Le **code de connexion** (commence par `FFBL1-`). Il contient déjà Client ID, Client Secret, lien webhook et le salon OMNE ; rien d'autre à envoyer.
2. Un rappel d'**accepter l'invitation de testeur** (si OMNE)

> Le code fonctionne comme un mot de passe : celui qui l'a peut écrire dans le salon. S'il fuite, supprimez le webhook dans Discord et regénérez le code. FFBL **oublie le code** dès que le personnage se déconnecte ; les joueurs le recollent à chaque session.

Puis renvoyez-les vers le [Guide du joueur](usuarios.fr.md).

## Problèmes courants

| Symptôme | Que faire |
|---|---|
| `invalid_request` / « Missing redirect_uri » à la connexion | Dans le portail Discord, dans l'app utilisée : **OAuth2 → Redirects**, ajoutez `http://localhost` et cliquez sur **Save Changes**. Vérifiez aussi que le Client ID est celui de la bonne app. |
| Le bot se ferme aussitôt | Relancez `INSTALAR.bat` et envoyez une capture de l'erreur. |
| « Token invalide » | Générez un nouveau token (**Bot → Reset Token**) et supprimez `token.txt`. |
| Le bot ignore les commandes | Vérifiez **Message Content Intent** et l'identifiant du salon. |
| Pas de musique | Vérifiez que vous êtes dans le salon vocal et que le bot tourne. |

Des questions ? Ouvrez une **Issue** avec capture (masquez token et webhook !).
