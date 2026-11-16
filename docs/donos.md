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
2. Nas permissões, marque: *View Channels*, *Send Messages*, *Embed Links*, *Attach Files*, *Read Message History*, *Connect*, *Speak*.
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
2. Baixe a pasta **`bot`** deste repositório (botão verde **Code → Download ZIP** e extraia).
3. Dê dois cliques em **`INSTALAR.bat`** (só na primeira vez; ele baixa o que o bot precisa).
4. Dê dois cliques em **`INICIAR.bat`**.
5. Na primeira vez ele pede o **token do bot** e o **ID da sala**. Cole cada um e aperte Enter. Ficam salvos.
6. Entre na sala de voz e digite `/tocar` (ou `!tocar nome da música` no chat da sala de voz). O bot entra sozinho na sua sala. Use `/teste` para ouvir um apito e conferir o som.

> Os arquivos `token.txt` e `config.json` ficam **só no seu computador** e já estão bloqueados no `.gitignore`.

## Parte 3: passar os dados ao grupo

Envie **em privado** para cada jogador:

1. **Client ID**
2. **Client Secret**
3. **Link do webhook**
4. Um aviso para **aceitar o convite de testador** (se usarem o OMNE)

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
