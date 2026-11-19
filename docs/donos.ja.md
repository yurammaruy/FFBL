[🇧🇷 Português](donos.md) · [🇺🇸 English](donos.en.md) · **🇯🇵 日本語** · [🇩🇪 Deutsch](donos.de.md) · [🇫🇷 Français](donos.fr.md)

# 🏠 チャンネルオーナー向けガイド

**ルームを用意する人**向けです。Discordの設定、音楽ボットの起動、仲間への接続情報の共有を行います。作業は**最初の1回だけ**。あとは音楽を流したいときにボットを起動しておくだけです。

> 🔐 **鉄則：****Client Secret**、**ボットのトークン**、**Webhookリンク**はパスワードと同じです。仲間にだけ個別メッセージで送ってください。公開の場やGitHubには**絶対に**載せないでください。

## パート1：Discordの設定

### 1.1 アプリケーションを作成
1. <https://discord.com/developers/applications> を開き、**New Application**をクリック。名前を付けます（例：*FFBL*）。
2. **General Information**で**Application ID**をコピー（これが**Client ID**）。
3. **OAuth2**で**Reset Secret**をクリックし、**Client Secret**をコピー（一度しか表示されません）。
4. **OAuth2 → Redirects**に `http://localhost` を追加して保存。

### 1.2 ボットを作成
1. **Bot**メニューで**Reset Token**をクリックし、**トークン**をコピー（大切に保管）。
2. 下の方にある**Message Content Intent**をオンにして保存。

### 1.3 ボットをサーバーに招待
1. **OAuth2 → URL Generator**でスコープ `bot` と `applications.commands` にチェック。
2. 権限：*View Channels*、*Send Messages*、*Embed Links*、*Attach Files*、*Read Message History*、*Manage Messages*、*Connect*、*Speak*。
3. 生成されたリンクを開き、サーバーを選択。

### 1.4 ルームとWebhook
1. Discordで**設定 → 詳細設定 → 開発者モード**をオン。
2. コマンド用の**テキストルーム**を作成（または選択）。右クリック → **チャンネルIDをコピー**して保管。
3. そのテキストルームで**チャンネルの編集 → 連携サービス → ウェブフック → 新しいウェブフック → ウェブフックURLをコピー**。これが**Webhookリンク**です。
4. 音楽を流す**ボイスルーム**を作成。

### 1.5 テスター（OMNEを使う場合のみ）
近接ボイスを使う場合のみ：ポータルの**App Testers**で友達を1人ずつ招待（最大**50人**）。各自が**招待を承認**する必要があります。

## パート2：音楽ボットを起動

ボットは**あなたのPCで動くプログラム**です。**閉じていると音楽は流れません。**仲間が使っている間は開いたままにしてください。

1. **Python 3.11以降**をインストールします。スタートメニューで `PowerShell` と入力して開き、次のコマンドを貼り付けます。その後**PowerShellを一度閉じて開き直し**、`python --version` で確認します。

   ```
   winget install -e --id Python.Python.3.12
   ```
   `winget` が使えない場合は、Microsoft Storeで「Python 3.12」を検索してインストールしてください。
2. ワンクリックでボットだけをダウンロード：[**FFBL-bot.zip**](https://github.com/yurammaruy/FFBL/releases/latest/download/FFBL-bot.zip)。ファイルを右クリック → **すべて展開**。（ファイルは数個だけです。リポジトリ全体をダウンロードする必要はありません。）
3. **`INSTALAR.bat`**をダブルクリック（初回のみ。必要なものをダウンロードします）。
4. **`INICIAR.bat`** をダブルクリック。**ボットのパネル**が開きます。
5. パネルで**+ Nova sala**（新しいルーム）をクリックし、順に入力します：**Nome do Canal**（チャンネル名）、**Nome da Sala**（ルーム名）、**Client ID**、**Client Secret**、**ID da SALA**（ルームID）、**Token do bot**、**Link do Webhook**。**Salvar**（保存）をクリックすると入力欄がロックされ（変更は**Editar informacoes**）、**接続コード**が表示されます。ルームごとにタブがあり、**同時にオンにできるのは1つだけ**です。
6. ルームのタブで**Iniciar bot**（ボット起動）をクリック。パネルを**最小化**すると時計の近くに移動し、ボットは動き続けます。**Basico**タブはボットの動きを簡単な文で、**Detalhado**タブはすべてを表示します。
7. ボイスルームに入り、`/tocar`（またはボイスルームのチャットで `!tocar 曲名`）を入力。ボットが自動で参加します。`/teste`でビープ音を鳴らし、音を確認できます。

> `token.txt`と`config.json`は**あなたのPCにだけ**保存され、`.gitignore`で既にアップロード対象外です。

## パート3：仲間に情報を共有

ボットパネルのルームのタブで、**接続コード**の下の**Copiar**ボタンを押し、各プレイヤーに**個別に**送ります：

1. **接続コード**（`FFBL1-`で始まります）。Client ID、Client Secret、Webhookリンク、OMNEのルームがすべて入っているので、他に送るものはありません。
2. **テスター招待を承認する**よう案内（OMNEを使う場合）

> コードはパスワードと同じです。持っている人はルームに書き込めます。漏れたらDiscordでWebhookを削除し、コードを作り直してください。FFBLはキャラクターがログアウトするたびに**コードを忘れる**ため、プレイヤーはセッションごとに貼り直します。

その後、[プレイヤー向けガイド](usuarios.ja.md)を案内してください。

## よくある問題

| 症状 | 対処 |
|---|---|
| 接続時に `invalid_request` / 「Missing redirect_uri」 | Discordポータルで使用中のアプリの **OAuth2 → Redirects** に `http://localhost` を追加し、**Save Changes** をクリック。Client IDが正しいアプリのものかも確認してください。 |
| ボットがすぐ閉じる | `INSTALAR.bat`を再実行し、エラーのスクリーンショットを送る。 |
| 「無効なトークン」 | 新しいトークンを発行（**Bot → Reset Token**）し、`token.txt`を削除。 |
| ボットがコマンドに反応しない | **Message Content Intent**とルームIDを確認。 |
| 音楽が流れない | ボイスルームに入っているか、ボットが起動しているか確認。 |

質問があれば、スクリーンショット付きで**Issue**を作成してください（トークンとWebhookは隠して！）。
