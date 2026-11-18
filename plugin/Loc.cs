namespace FFBL;

/// <summary>Traducao dos textos do plugin. A chave e o texto em portugues (o idioma padrao).</summary>
internal static class Loc
{
    public static string Idioma = "pt";

    public static readonly (string Codigo, string Nome)[] Idiomas =
    {
        ("pt", "Portugu\u00eas"),
        ("en", "English"),
        ("ja", "\u65e5\u672c\u8a9e"),
        ("de", "Deutsch"),
        ("fr", "Fran\u00e7ais"),
    };

    private static int Indice => Idioma switch { "en" => 0, "ja" => 1, "de" => 2, "fr" => 3, _ => -1 };

    private static readonly Dictionary<string, string[]> Tabela = new()
    {
        { "Codigo de conexao (quem criou a sala te passa)", new[] { "Connection code (from whoever set up the room)", "接続コード（ルームを作った人から受け取る）", "Verbindungscode (vom Ersteller des Raums)", "Code de connexion (donné par le créateur de la salle)" } },
        { "Aplicar codigo", new[] { "Apply code", "コードを適用", "Code anwenden", "Appliquer le code" } },
        { "Preencher a mao (avancado)", new[] { "Fill in manually (advanced)", "手動で入力（上級者向け）", "Manuell ausfüllen (erweitert)", "Remplir à la main (avancé)" } },
        { "Projecao", new[] { "Projection", "投影", "Projektion", "Projection" } },
        { "Idioma", new[] { "Language", "言語", "Sprache", "Langue" } },
        { "Telao", new[] { "Screen", "スクリーン", "Leinwand", "Écran" } },
        { "    sem personagem: o volume desta pessoa nao e alterado", new[] { "    no character: this person's volume is not changed", "    キャラ未設定：この人の音量は変更されません", "    kein Charakter: Lautstärke dieser Person bleibt unverändert", "    pas de personnage : le volume de cette personne n'est pas modifié" } },
        { "(icone para abrir o FFBL)", new[] { "(icon to open FFBL)", "(FFBLを開くアイコン)", "(Symbol zum Öffnen von FFBL)", "(icône pour ouvrir FFBL)" } },
        { "(invertido)", new[] { "(inverted)", "(反転)", "(invertiert)", "(inversé)" } },
        { "(normal)", new[] { "(normal)", "(通常)", "(normal)", "(normal)" } },
        { "(nenhuma imagem escolhida: mostra a imagem padrao)", new[] { "(no image chosen: shows the default image)", "(画像未選択：標準画像を表示)", "(kein Bild gewählt: zeigt das Standardbild)", "(aucune image choisie : affiche l'image par défaut)" } },
        { "(ninguem ainda)", new[] { "(nobody yet)", "(まだ誰もいません)", "(noch niemand)", "(personne pour l'instant)" } },
        { "100% = volume normal do Discord.", new[] { "100% = normal Discord volume.", "100% = Discordの通常音量。", "100 % = normale Discord-Lautstärke.", "100 % = volume normal de Discord." } },
        { "A imagem tambem vai para o chat do Discord (ate 10 MB), para quem nao tem o FFBL.", new[] { "The image is also posted to the Discord chat (up to 10 MB) for people without FFBL.", "画像はDiscordのチャットにも送られます（10MBまで）。FFBLを持っていない人向けです。", "Das Bild wird auch im Discord-Chat gepostet (bis 10 MB), für alle ohne FFBL.", "L'image est aussi envoyée dans le chat Discord (jusqu'à 10 Mo) pour ceux qui n'ont pas FFBL." } },
        { "Ajuste da imagem na tela", new[] { "Image adjustment on the screen", "画面上の画像調整", "Bildanpassung auf der Leinwand", "Réglage de l'image sur l'écran" } },
        { "Ajustes so no seu jogo", new[] { "Adjustments only in your game", "あなたのゲーム内のみの調整", "Einstellungen nur in deinem Spiel", "Réglages uniquement dans votre jeu" } },
        { "Altura", new[] { "Height", "高さ", "Höhe", "Hauteur" } },
        { "Alvo", new[] { "Target", "ターゲット", "Ziel", "Cible" } },
        { "Ao criar o telao, o FFBL invoca o Plush Cushion e ele vira a tela. Precisa do Penumbra.", new[] { "When you create the screen, FFBL summons the Plush Cushion and it becomes the screen. Requires Penumbra.", "スクリーンを作ると、FFBLがプラッシュクッションを呼び出し、それがスクリーンになります。Penumbraが必要です。", "Beim Erstellen ruft FFBL das Plüschkissen herbei und es wird zur Leinwand. Benötigt Penumbra.", "À la création, FFBL invoque le Coussin en peluche et il devient l'écran. Nécessite Penumbra." } },
        { "Aperte MENU na roda para abrir a playlist.", new[] { "Press MENU on the wheel to open the playlist.", "ホイールのMENUを押してプレイリストを開きます。", "Drücke MENU am Rad, um die Playlist zu öffnen.", "Appuyez sur MENU sur la molette pour ouvrir la playlist." } },
        { "Ativar a proximidade", new[] { "Enable proximity", "近接機能を有効にする", "Nähe aktivieren", "Activer la proximité" } },
        { "Ativar o OMNE", new[] { "Enable OMNE", "OMNEを有効にする", "OMNE aktivieren", "Activer OMNE" } },
        { "Brilho", new[] { "Brightness", "明るさ", "Helligkeit", "Luminosité" } },
        { "Brilho: luz propria da tela, para os dois lados ficarem claros.", new[] { "Brightness: the screen's own light, so both sides look bright.", "明るさ：画面自体の光で、両面を明るくします。", "Helligkeit: Eigenlicht der Leinwand, damit beide Seiten hell sind.", "Luminosité : lumière propre à l'écran, pour que les deux faces soient claires." } },
        { "Cada linha: Nome do Personagem = ID do Discord. Copie (Ctrl+A, Ctrl+C) e envie ao grupo; quem recebe cola no lugar.", new[] { "Each line: Character Name = Discord ID. Copy (Ctrl+A, Ctrl+C) and send it to the group; whoever receives it pastes it in place.", "各行：キャラクター名 = Discord ID。コピー(Ctrl+A, Ctrl+C)してグループに送り、受け取った人は貼り付けます。", "Jede Zeile: Charaktername = Discord-ID. Kopieren (Strg+A, Strg+C), an die Gruppe senden; Empfänger fügen es ein.", "Chaque ligne : Nom du personnage = ID Discord. Copiez (Ctrl+A, Ctrl+C) et envoyez au groupe ; le destinataire colle à la place." } },
        { "Capa da musica atual", new[] { "Cover of the current song", "現在の曲のカバー", "Cover des aktuellen Songs", "Pochette du morceau actuel" } },
        { "Client ID (do portal do Discord)", new[] { "Client ID (from the Discord portal)", "クライアントID（Discordポータル）", "Client-ID (vom Discord-Portal)", "Client ID (du portail Discord)" } },
        { "Client Secret (do portal do Discord)", new[] { "Client Secret (from the Discord portal)", "クライアントシークレット（Discordポータル）", "Client-Secret (vom Discord-Portal)", "Client Secret (du portail Discord)" } },
        { "Compartilhar a lista de personagens", new[] { "Share the character list", "キャラクター一覧を共有", "Charakterliste teilen", "Partager la liste des personnages" } },
        { "Conectar", new[] { "Connect", "接続", "Verbinden", "Connecter" } },
        { "Conectar sozinho ao abrir o jogo", new[] { "Connect automatically when the game opens", "ゲーム起動時に自動で接続", "Beim Spielstart automatisch verbinden", "Se connecter automatiquement à l'ouverture du jeu" } },
        { "Conecte primeiro para criar um telao.", new[] { "Connect first to create a screen.", "スクリーンを作るには先に接続してください。", "Verbinde dich zuerst, um eine Leinwand zu erstellen.", "Connectez-vous d'abord pour créer un écran." } },
        { "Copiar Client ID e Secret do OMNE", new[] { "Copy Client ID and Secret from OMNE", "OMNEからクライアントIDとシークレットをコピー", "Client-ID und Secret von OMNE kopieren", "Copier le Client ID et le Secret d'OMNE" } },
        { "Ver o telao criado por outras pessoas", new[] { "See screens created by other people", "他の人が作成したスクリーンを見る", "Von anderen erstellte Leinwände sehen", "Voir les écrans créés par d'autres" } },
        { "O telao aparece no minion de quem o criou. O FFBL nao invoca nada no seu jogo.", new[] { "The screen appears on the creator's minion. FFBL does not summon anything in your game.", "スクリーンは作成者のミニオンに表示されます。FFBLがあなたのゲームで何かを呼び出すことはありません。", "Die Leinwand erscheint am Minion des Erstellers. FFBL ruft in deinem Spiel nichts herbei.", "L'écran apparaît sur la mascotte de son créateur. FFBL n'invoque rien dans votre jeu." } },
        { "Ha um telao ativo na sala, mas ele nao aparece no seu jogo porque voce desligou a opcao.", new[] { "There is an active screen in the room, but it is not shown because you turned the option off.", "ルームにスクリーンがありますが、オプションをオフにしているため表示されません。", "Im Raum ist eine Leinwand aktiv, wird aber nicht angezeigt, da du die Option ausgeschaltet hast.", "Un écran est actif dans le salon, mais il n'est pas affiché car vous avez désactivé l'option." } },
        { "Ja existe um telao sendo usado, invocado por {0} em {1} (X {2:F1}, Z {3:F1}).", new[] { "A screen is already in use, summoned by {0} in {1} (X {2:F1}, Z {3:F1}).", "すでにスクリーンが使用中です。{0}さんが{1}で設置（X {2:F1}、Z {3:F1}）。", "Es ist bereits eine Leinwand in Benutzung, erstellt von {0} in {1} (X {2:F1}, Z {3:F1}).", "Un écran est déjà utilisé, invoqué par {0} à {1} (X {2:F1}, Z {3:F1})." } },
        { "Ele nao aparece no seu jogo porque voce desligou a opcao acima.", new[] { "It is not shown in your game because you turned the option above off.", "上のオプションをオフにしているため、あなたのゲームには表示されません。", "Sie wird nicht angezeigt, da du die Option oben ausgeschaltet hast.", "Il n'est pas affiché car vous avez désactivé l'option ci-dessus." } },
        { "Se essa pessoa saiu do jogo, voce pode liberar o telao.", new[] { "If that person left the game, you can release the screen.", "その人がゲームを終了した場合は、スクリーンを解放できます。", "Hat die Person das Spiel verlassen, kannst du die Leinwand freigeben.", "Si cette personne a quitté le jeu, vous pouvez libérer l'écran." } },
        { "Liberar telao", new[] { "Release screen", "スクリーンを解放", "Leinwand freigeben", "Libérer l'écran" } },
        { "Criar telao aqui", new[] { "Create screen here", "ここにスクリーンを作成", "Leinwand hier erstellen", "Créer l'écran ici" } },
        { "Desativado.", new[] { "Disabled.", "無効です。", "Deaktiviert.", "Désactivé." } },
        { "Desconectar", new[] { "Disconnect", "切断", "Trennen", "Déconnecter" } },
        { "Detalhes tecnicos e ajuste fino", new[] { "Technical details and fine tuning", "技術的な詳細と微調整", "Technische Details und Feinabstimmung", "Détails techniques et réglage fin" } },
        { "Dist.", new[] { "Dist.", "距離", "Entf.", "Dist." } },
        { "Entrar na sala", new[] { "Join the room", "ルームに参加", "Raum betreten", "Rejoindre le salon" } },
        { "Escreva o nome do personagem de cada pessoa (como aparece no jogo). Dica: botao Alvo.", new[] { "Type each person's character name (as shown in game). Tip: Target button.", "各自のキャラクター名をゲーム内の表記どおりに入力してください。ヒント：ターゲットボタン。", "Gib den Charakternamen jeder Person ein (wie im Spiel). Tipp: Ziel-Knopf.", "Saisissez le nom du personnage de chacun (comme en jeu). Astuce : bouton Cible." } },
        { "Esp. Z", new[] { "Thick. Z", "厚さ Z", "Dicke Z", "Épais. Z" } },
        { "Esquecer autorizacao", new[] { "Forget authorization", "認証を忘れる", "Autorisierung vergessen", "Oublier l'autorisation" } },
        { "Eu", new[] { "Me", "自分", "Ich", "Moi" } },
        { "Giro", new[] { "Rotation", "回転", "Drehung", "Rotation" } },
        { "ID da sala de voz do OMNE", new[] { "OMNE voice room ID", "OMNEのボイスルームID", "ID des OMNE-Sprachraums", "ID du salon vocal OMNE" } },
        { "Imagem padrao (FFBL)", new[] { "Default image (FFBL)", "標準画像（FFBL）", "Standardbild (FFBL)", "Image par défaut (FFBL)" } },
        { "Inverter frente e verso", new[] { "Swap front and back", "表と裏を入れ替え", "Vorder- und Rückseite tauschen", "Inverser avant et arrière" } },
        { "Lado", new[] { "Side", "横", "Seite", "Côté" } },
        { "Lado de tras preto (pode piscar ao dar a volta)", new[] { "Black back side (may flicker when walking around)", "裏面を黒にする（回り込むとちらつくことがあります）", "Rückseite schwarz (kann beim Umrunden flackern)", "Face arrière noire (peut clignoter en tournant autour)" } },
        { "Largura", new[] { "Width", "幅", "Breite", "Largeur" } },
        { "Mostrar grade de teste", new[] { "Show test grid", "テストグリッドを表示", "Testgitter anzeigen", "Afficher la grille de test" } },
        { "Mover X", new[] { "Move X", "移動 X", "Verschieben X", "Déplacer X" } },
        { "Mover Y", new[] { "Move Y", "移動 Y", "Verschieben Y", "Déplacer Y" } },
        { "Mover para onde estou", new[] { "Move to where I am", "自分の位置へ移動", "Zu meiner Position verschieben", "Déplacer là où je suis" } },
        { "No Discord: botao direito na sala > Copiar ID do canal.", new[] { "In Discord: right-click the room > Copy Channel ID.", "Discordでルームを右クリック > チャンネルIDをコピー。", "In Discord: Rechtsklick auf den Raum > Kanal-ID kopieren.", "Dans Discord : clic droit sur le salon > Copier l'identifiant du salon." } },
        { "O link do webhook funciona como senha: nao mostre nem compartilhe. No Discord, seu nome aparece como o do seu personagem.", new[] { "The webhook link works like a password: don't show or share it. On Discord, your name appears as your character's name.", "Webhookのリンクはパスワードと同じです。表示や共有はしないでください。Discordではキャラクター名で表示されます。", "Der Webhook-Link wirkt wie ein Passwort: nicht zeigen oder teilen. In Discord erscheint dein Charaktername.", "Le lien du webhook fonctionne comme un mot de passe : ne le montrez pas. Sur Discord, votre nom apparaît comme celui de votre personnage." } },
        { "O que mostrar no telao", new[] { "What to show on the screen", "スクリーンに表示するもの", "Was auf der Leinwand gezeigt wird", "Que montrer sur l'écran" } },
        { "OMNE - voz por proximidade", new[] { "OMNE - proximity voice", "OMNE - 近接ボイス", "OMNE - Näheschat", "OMNE - voix de proximité" } },
        { "Parado", new[] { "Stopped", "停止中", "Gestoppt", "Arrêté" } },
        { "Penumbra: ", new[] { "Penumbra: ", "Penumbra: ", "Penumbra: ", "Penumbra : " } },
        { "respondeu", new[] { "responded", "応答あり", "hat geantwortet", "a répondu" } },
        { "nao respondeu", new[] { "did not respond", "応答なし", "hat nicht geantwortet", "n'a pas répondu" } },
        { "Procurar imagem...", new[] { "Browse image...", "画像を選択...", "Bild suchen...", "Parcourir l'image..." } },
        { "Proximidade (volume pela distancia)", new[] { "Proximity (volume by distance)", "近接（距離による音量）", "Nähe (Lautstärke nach Entfernung)", "Proximité (volume selon la distance)" } },
        { "Quem e quem na sala do OMNE", new[] { "Who is who in the OMNE room", "OMNEルームの参加者", "Wer ist wer im OMNE-Raum", "Qui est qui dans le salon OMNE" } },
        { "Reaplicar", new[] { "Reapply", "再適用", "Erneut anwenden", "Réappliquer" } },
        { "Recolher tela", new[] { "Put away screen", "スクリーンを片付ける", "Leinwand einpacken", "Ranger l'écran" } },
        { "Restaurar o volume de todos (100%)", new[] { "Restore everyone's volume (100%)", "全員の音量を戻す(100%)", "Lautstärke aller zurücksetzen (100 %)", "Rétablir le volume de tous (100 %)" } },
        { "Rot X", new[] { "Rot X", "回転 X", "Rot. X", "Rot. X" } },
        { "Rot Z", new[] { "Rot Z", "回転 Z", "Rot. Z", "Rot. Z" } },
        { "Subir", new[] { "Raise", "上下", "Höhe +/-", "Monter" } },
        { "Tamanho do iPod", new[] { "iPod size", "iPodのサイズ", "iPod-Größe", "Taille de l'iPod" } },
        { "Telao de: ", new[] { "Screen of: ", "スクリーンの持ち主：", "Leinwand von: ", "Écran de : " } },
        { "Telao no mundo (seu Plush Cushion)", new[] { "Screen in the world (your Plush Cushion)", "ワールド上のスクリーン（あなたのプラッシュクッション）", "Leinwand in der Welt (dein Plüschkissen)", "Écran dans le monde (votre Coussin en peluche)" } },
        { "Uma imagem do meu computador", new[] { "An image from my computer", "パソコンの画像", "Ein Bild von meinem Computer", "Une image de mon ordinateur" } },
        { "Usar o mesmo Client ID e Secret do FFBL", new[] { "Use the same Client ID and Secret as FFBL", "FFBLと同じクライアントIDとシークレットを使う", "Dieselbe Client-ID und dasselbe Secret wie FFBL verwenden", "Utiliser le même Client ID et Secret que FFBL" } },
        { "Use se a imagem aparecer cortada ou deslocada.", new[] { "Use this if the image looks cropped or shifted.", "画像が切れたりずれたりした場合に使います。", "Verwenden, wenn das Bild abgeschnitten oder verschoben aussieht.", "À utiliser si l'image paraît coupée ou décalée." } },
        { "Vale so para voce: cada pessoa escolhe o que ve.", new[] { "Only for you: each person chooses what they see.", "あなただけに有効：各自が表示内容を選びます。", "Nur für dich: jeder wählt selbst, was er sieht.", "Valable pour vous seul : chacun choisit ce qu'il voit." } },
        { "Virar imagem na horizontal (alem do automatico)", new[] { "Flip image horizontally (besides the automatic one)", "画像を左右反転（自動に加えて）", "Bild horizontal spiegeln (zusätzlich zur automatischen)", "Retourner l'image horizontalement (en plus de l'automatique)" } },
        { "Virar imagem na vertical", new[] { "Flip image vertically", "画像を上下反転", "Bild vertikal spiegeln", "Retourner l'image verticalement" } },
        { "Zerar ajuste", new[] { "Reset adjustment", "調整をリセット", "Anpassung zurücksetzen", "Réinitialiser le réglage" } },
        { "Zoom X", new[] { "Zoom X", "ズーム X", "Zoom X", "Zoom X" } },
        { "Zoom Y", new[] { "Zoom Y", "ズーム Y", "Zoom Y", "Zoom Y" } },
        { "Macro: esconder o icone", new[] { "Macro: hide the icon", "マクロ：アイコンを隠す", "Makro: Symbol ausblenden", "Macro : masquer l'icône" } },
        { "Macro: mostrar o icone", new[] { "Macro: show the icon", "マクロ：アイコンを表示", "Makro: Symbol anzeigen", "Macro : afficher l'icône" } },
        { "Tocando agora", new[] { "Now playing", "再生中", "Läuft gerade", "Lecture en cours" } },
        { "Pausado", new[] { "Paused", "一時停止中", "Pausiert", "En pause" } },
        { "Fila vazia", new[] { "Queue is empty", "キューは空です", "Warteschlange leer", "File d'attente vide" } },
        { "sem capa", new[] { "no cover", "カバーなし", "kein Cover", "pas de pochette" } },
        { "Nome da musica ou link do YouTube:", new[] { "Song name or YouTube link:", "曲名またはYouTubeリンク：", "Songname oder YouTube-Link:", "Nom du morceau ou lien YouTube :" } },
        { "ENTER = pedir     ESC = cancelar", new[] { "ENTER = request     ESC = cancel", "ENTER = リクエスト     ESC = キャンセル", "ENTER = wünschen     ESC = abbrechen", "ENTRÉE = demander     ÉCHAP = annuler" } },
        { "Procurando o Discord...", new[] { "Looking for Discord...", "Discordを探しています...", "Suche Discord...", "Recherche de Discord..." } },
        { "Entrando na sala...", new[] { "Joining the room...", "ルームに参加中...", "Betrete den Raum...", "Connexion au salon..." } },
        { "Conectado como ", new[] { "Connected as ", "接続済み：", "Verbunden als ", "Connecté en tant que " } },
        { "Olhe a janela do Discord e clique em Autorizar...", new[] { "Look at the Discord window and click Authorize...", "Discordのウィンドウを見て「認証」をクリックしてください...", "Sieh dir das Discord-Fenster an und klicke auf Autorisieren...", "Regardez la fenêtre Discord et cliquez sur Autoriser..." } },
        { "Desconectado", new[] { "Disconnected", "切断されました", "Getrennt", "Déconnecté" } },
        { "Conecte primeiro.", new[] { "Connect first.", "先に接続してください。", "Bitte zuerst verbinden.", "Connectez-vous d'abord." } },
        { "Personagem ainda nao carregou.", new[] { "Character has not loaded yet.", "キャラクターがまだ読み込まれていません。", "Charakter noch nicht geladen.", "Le personnage n'est pas encore chargé." } },
        { "O link do webhook esta vazio ou invalido.", new[] { "The webhook link is empty or invalid.", "Webhookのリンクが空か無効です。", "Der Webhook-Link ist leer oder ungültig.", "Le lien du webhook est vide ou invalide." } },
        { "Falta o Client ID.", new[] { "Client ID is missing.", "クライアントIDがありません。", "Client-ID fehlt.", "Le Client ID est manquant." } },
        { "Falta o Client Secret.", new[] { "Client Secret is missing.", "クライアントシークレットがありません。", "Client-Secret fehlt.", "Le Client Secret est manquant." } },
        { "Sem telao criado.", new[] { "No screen created.", "スクリーンが作成されていません。", "Keine Leinwand erstellt.", "Aucun écran créé." } },
        { "O telao esta em outro lugar.", new[] { "The screen is somewhere else.", "スクリーンは別の場所にあります。", "Die Leinwand ist woanders.", "L'écran est ailleurs." } },
        { "Telao ativo no seu minion.", new[] { "Screen active on your minion.", "スクリーンがミニオンで有効です。", "Leinwand auf deinem Begleiter aktiv.", "Écran actif sur votre mascotte." } },
        { "Preparando o minion...", new[] { "Preparing the minion...", "ミニオンを準備中...", "Begleiter wird vorbereitet...", "Préparation de la mascotte..." } },
        { "Preparando a imagem...", new[] { "Preparing the image...", "画像を準備中...", "Bild wird vorbereitet...", "Préparation de l'image..." } },
        { "Aguardando o minion.", new[] { "Waiting for the minion.", "ミニオンを待っています。", "Warte auf den Begleiter.", "En attente de la mascotte." } },
        { "Invocando o Plush Cushion...", new[] { "Summoning the Plush Cushion...", "プラッシュクッションを呼び出し中...", "Plüschkissen wird beschworen...", "Invocation du Coussin en peluche..." } },
        { "Nao consegui invocar o Plush Cushion. Invoque pelo menu de minions.", new[] { "Couldn't summon the Plush Cushion. Summon it from the minion menu.", "プラッシュクッションを呼び出せませんでした。ミニオンメニューから呼び出してください。", "Plüschkissen konnte nicht beschworen werden. Beschwöre es über das Begleitermenü.", "Impossible d'invoquer le Coussin en peluche. Invoquez-le depuis le menu des mascottes." } },
        { "O Penumbra nao respondeu. Ele esta instalado e ligado?", new[] { "Penumbra did not respond. Is it installed and enabled?", "Penumbraが応答しません。インストールされ有効になっていますか？", "Penumbra hat nicht geantwortet. Ist es installiert und aktiv?", "Penumbra n'a pas répondu. Est-il installé et activé ?" } },
        { "O Penumbra recusou a troca (veja os detalhes tecnicos).", new[] { "Penumbra refused the swap (see the technical details).", "Penumbraが変更を拒否しました（技術的な詳細を確認）。", "Penumbra hat den Tausch abgelehnt (siehe technische Details).", "Penumbra a refusé l'échange (voir les détails techniques)." } },
        { "Nao achei o Plush Cushion na lista de minions do jogo.", new[] { "Couldn't find the Plush Cushion in the game's minion list.", "ゲームのミニオン一覧にプラッシュクッションが見つかりません。", "Plüschkissen nicht in der Begleiterliste gefunden.", "Coussin en peluche introuvable dans la liste des mascottes." } },
        { "Nao consegui montar a imagem: ", new[] { "Couldn't build the image: ", "画像を作成できませんでした：", "Bild konnte nicht erstellt werden: ", "Impossible de créer l'image : " } },
        { "Erro: ", new[] { "Error: ", "エラー：", "Fehler: ", "Erreur : " } },
        { "Idioma do FFBL", new[] { "FFBL language", "FFBLの言語", "FFBL-Sprache", "Langue de FFBL" } },
        { "Escolha o idioma dos textos do plugin.", new[] { "Choose the language of the plugin's texts.", "プラグインの表示言語を選んでください。", "Wähle die Sprache der Plugin-Texte.", "Choisissez la langue des textes du plugin." } },
        { "Textos vindos do bot e do Discord continuam no idioma original.", new[] { "Texts coming from the bot and Discord stay in their original language.", "ボットとDiscordからのテキストは元の言語のままです。", "Texte vom Bot und von Discord bleiben in der Originalsprache.", "Les textes venant du bot et de Discord restent dans leur langue d'origine." } },
    };

    /// <summary>Traduz um texto (aceita o sufixo de identificador do ImGui, como "Conectar##omne").</summary>
    public static string T(string pt)
    {
        var i = Indice;
        if (i < 0 || string.IsNullOrEmpty(pt)) return pt;
        var corte = pt.IndexOf("##", StringComparison.Ordinal);
        var texto = corte >= 0 ? pt[..corte] : pt;
        var sufixo = corte >= 0 ? pt[corte..] : "";
        if (Tabela.TryGetValue(texto, out var t)) return t[i] + sufixo;
        // textos que comecam com uma frase conhecida (ex.: "Conectado como Fulano")
        foreach (var (k, v) in Tabela)
            if (k.EndsWith(' ') && texto.StartsWith(k, StringComparison.Ordinal)) return v[i] + texto[k.Length..] + sufixo;
        return pt;
    }
}
