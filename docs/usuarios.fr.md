[🇧🇷 Português](usuarios.md) · [🇺🇸 English](usuarios.en.md) · [🇯🇵 日本語](usuarios.ja.md) · [🇩🇪 Deutsch](usuarios.de.md) · **🇫🇷 Français**

# 🎧 Guide du joueur

Pour celles et ceux qui **utilisent** FFBL en jeu. Pour monter le salon sur Discord, lisez le [Guide du propriétaire](donos.fr.md).

## Ce qu'il vous faut

- Final Fantasy XIV avec **XIVLauncher/Dalamud**.
- L'**application Discord** ouverte sur le même PC (pas seulement le navigateur).
- Trois éléments envoyés en privé par le **propriétaire** : **Client ID**, **Client Secret**, **lien webhook** (à traiter comme un mot de passe !).
- Pour l'**écran** (optionnel) : le plugin **Penumbra** et la mascotte **Plush Cushion**.

> 🎧 **Vous voulez juste écouter ?** Rien à installer : rejoignez le salon vocal Discord.

## Étape 1 : accepter l'invitation de testeur
Le propriétaire vous ajoute comme **testeur de l'app**. Discord vous envoie une invitation : **acceptez-la**. Sans cela, Discord refuse la connexion du plugin.

## Étape 2 : installer le plugin
1. En jeu, tapez `/xlsettings`.
2. Allez dans **Experimental**.
3. Dans **Dépôts de plugins personnalisés**, collez l'adresse ci-dessous, cliquez sur **+**, puis **Enregistrer** :

```
https://raw.githubusercontent.com/yurammaruy/FFBL/main/repo.json
```

4. Tapez `/xlplugins`, cherchez **FFBL** dans *Tous les plugins* et cliquez sur **Installer**.

## Étape 3 : se connecter
1. Tapez `/ffbl` pour ouvrir l'iPod.
2. Sur l'écran de connexion, renseignez **Client ID**, **Client Secret** et **Webhook**.
3. Cliquez sur **Connecter**.
4. Regardez la fenêtre **Discord** et cliquez sur **Autoriser**.
5. Quand *Connecté en tant que (votre nom)* s'affiche, appuyez sur **MENU** sur la molette pour ouvrir la playlist.

## Utilisation

| Bouton de la molette | Action |
|---|---|
| **MENU** | Alterne entre connexion et playlist |
| ⏮ | Morceau précédent |
| ⏭ | Passer |
| ⏯ | Lecture / pause |
| **Bouton central** | Ouvre le champ de demande (nom ou lien YouTube) |
| **+ / −** | Volume |
| Icône en bas | Ferme la fenêtre (comme `/ffbl`) |

Changez la langue du plugin dans l'onglet **Idioma**.

## Écran (optionnel)
1. Installez **Penumbra** et laissez-le activé.
   Ensuite, **importez une fois** le fichier [`penumbra/Background_Screen.pmp`](../penumbra/Background_Screen.pmp) : ouvrez Penumbra et glissez le fichier dans sa fenêtre (ou utilisez son bouton d'import). Une seule fois suffit ; ne changez rien d'autre.
2. Dans l'onglet **Projeção**, cliquez sur le bouton de création de l'écran. FFBL invoque votre *Plush Cushion* et le transforme en écran.
3. Choisissez : image par défaut, pochette ou image de votre PC.
4. Réglez taille, position et rotation avec **−** et **+**.

Ceux qui n'ont pas FFBL voient toujours la mascotte normale.

## OMNE (voix de proximité)
Dans l'onglet **OMNE**, **désactivé par défaut**. Explication sur la [page principale](../README.fr.md). Activez-le quand le groupe veut utiliser la voix de proximité.

## Problèmes courants

| Symptôme | Que faire |
|---|---|
| `invalid_request` / « Missing redirect_uri » à la connexion | Dans le portail Discord, dans l'app utilisée : **OAuth2 → Redirects**, ajoutez `http://localhost` et cliquez sur **Save Changes**. Vérifiez aussi que le Client ID est celui de la bonne app. |
| Bloqué sur « Recherche de Discord » | Ouvrez l'application Discord (pas seulement le navigateur) et réessayez. |
| Pas de demande d'autorisation | Vérifiez que vous avez accepté l'invitation de testeur. |
| Lien webhook invalide | Collez le lien complet, qui commence par `https://discord.com/api/webhooks/`. |
| L'écran n'apparaît pas | Vérifiez que Penumbra est installé et activé. |
| Pas de musique | Demandez au propriétaire de vérifier que le bot tourne. |

Rien ne marche ? Ouvrez une **Issue** ici sur GitHub avec une capture d'écran.
