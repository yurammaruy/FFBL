🇧🇷 **Português** · [🇺🇸 English](donos.en.md) · [🇯🇵 日本語](donos.ja.md) · [🇩🇪 Deutsch](donos.de.md) · [🇫🇷 Français](donos.fr.md)

# 🏠 Guia do dono do canal

Este guia é para quem vai **montar a sala**: configurar o Discord, ligar o bot de música e dar aos amigos os dados para se conectarem. Você faz isso **uma vez**; depois é só manter o bot ligado quando o grupo quiser música.

> 🔐 **Regra de ouro:** o **Client Secret**, o **token do bot** e o **link do webhook** funcionam como senhas. Envie só em mensagem privada para o seu grupo. **Nunca** publique em canais abertos nem no GitHub.

## Parte 1: configurar o Discord

### 1.1 Criar o aplicativo
1. Abra <https://discord.com/developers/applications> e clique em **New Application**. Dê um nome (ex.: *FFBL*).
2. Em **General Information**, copie o **Application ID** (é o **Client ID**).
3. Em **OAuth2**, clique em **Reset Secret** e copie o **Client Secret** (aparece uma vez só).
4. Em **OAuth2 → Redirects**, adicione `http://localhost` e salve.

### 1.2 Criar o bot
1. No menu **Bot**, clique em **Reset Token** e copie o **token** (guarde bem).
2. Mais abaixo, ligue **Message Content Intent** e salve.

### 1.3 Convidar o bot para o seu servidor
1. Em **OAuth2 → URL Generator**, marque os escopos `bot` e `applications.commands`.
2. Nas permissões, marque: *View Channels*, *Send Messages*, *Embed Links*, *Attach Files*, *Read Message History*, *Manage Messages*, *Connect*, *Speak*.
3. Abra o link gerado e escolha o seu servidor.

### 1.4 Salas e webhook
1. No Discord: **Configurações → Avançado → Modo desenvolvedor** (ligue).
2. Crie (ou escolha) uma **sala de texto** para os comandos. Clique com o botão direito nela → **Copiar ID da sala**. Guarde.
3. Nessa sala de texto: **Editar canal → Integrações → Webhooks → Novo webhook → Copiar URL**. Esse é o **link do webhook**.
4. Crie uma **sala de voz** onde a música vai tocar.

### 1.5 Testadores (só para o OMNE)
Só se for usar a voz por proximidade: no portal, **App Testers**, convide cada amigo (máximo **50**). Cada um precisa **aceitar o convite**.

## Parte 2: ligar o bot de música

O bot é um programa que **roda no seu computador**. **Se ele estiver fechado, não toca música.** Deixe-o aberto enquanto o grupo estiver usando.

1. Instale o **Python 3.11 ou mais novo**. Abra o menu Iniciar, digite `PowerShell`, abra e cole o comando abaixo. Depois **feche e abra o PowerShell de novo** e confira com `python --version`.

   ```
   winget install -e --id Python.Python.3.12
   ```
   Se o `winget` não existir no seu Windows, instale o Python pela Microsoft Store (procure por "Python 3.12").
2. Baixe só o bot com um clique: [**FFBL-bot.zip**](https://github.com/yurammaruy/FFBL/releases/latest/download/FFBL-bot.zip). Clique com o botão direito no arquivo → **Extrair tudo**. (São poucos arquivos; não precisa baixar o repositório inteiro.)
3. Dê dois cliques em **`INSTALAR.bat`** (só na primeira vez; ele baixa o que o bot precisa).
4. Dê dois cliques em **`INICIAR.bat`**. Abre o **painel do bot**.
5. No painel, clique em **+ Nova sala**. Preencha, nesta ordem: **Nome do Canal**, **Nome da Sala**, **Client ID**, **Client Secret**, **ID da SALA**, **Token do bot** e **Link do Webhook**. Clique em **Salvar**: os campos ficam travados (para mudar, use **Editar informações**) e aparece o **código de conexão**. Cada sala ganha a sua aba, e **só uma fica ligada por vez**.
6. Clique em **Iniciar bot** na aba da sala. Se você **minimizar** o painel, ele vai para perto do relógio e o bot continua tocando. A aba **Básico** mostra, em frases simples, o que o bot está fazendo; a **Detalhado** mostra tudo.
7. Entre na sala de voz e digite `/tocar` (ou `!tocar nome da música` no chat da sala de voz). O bot entra sozinho na sua sala. Use `/teste` para ouvir um apito e conferir o som.

> Os arquivos `token.txt`, `config.json` e `salas.json` ficam **só no seu computador** e já estão bloqueados no `.gitignore`. O **token do bot** nunca vai dentro do código de conexão.

## Parte 3: passar os dados ao grupo

No painel do bot, na aba da sala, use o botão **Copiar** abaixo do **código de conexão** e envie **em privado** para cada jogador:

1. O **código de conexão** (começa com `FFBL1-`). Ele já leva Client ID, Client Secret, link do webhook e a sala do OMNE, então não precisa mandar mais nada separado.
2. Um aviso para **aceitar o convite de testador** (se usarem o OMNE)

> O código funciona como senha: quem o tem escreve na sala. Se ele vazar, apague o webhook no Discord e gere o código de novo. O FFBL **esquece o código** sempre que o personagem desconecta, então o jogador precisa colar de novo a cada sessão.

Depois mande eles para o [Guia do jogador](usuarios.md).

## Problemas comuns

| Sintoma | O que fazer |
|---|---|
| `invalid_request` / "Missing redirect_uri" ao conectar | No portal do Discord, no app usado: **OAuth2 → Redirects**, adicione `http://localhost` e clique em **Save Changes**. Confira também se o Client ID é do app certo. |
| O bot fecha logo ao abrir | Rode `INSTALAR.bat` de novo e mande um print do erro. |
| "Token inválido" | Gere um token novo (**Bot → Reset Token**) e apague o `token.txt`. |
| O bot não responde aos comandos | Confira o **Message Content Intent** e o ID da sala de texto. |
| A música não toca | Confira se você está na sala de voz e se o bot está ligado. |

Dúvidas? Abra uma **Issue** com um print (sem mostrar token nem webhook!).
