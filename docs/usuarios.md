🇧🇷 **Português** · [🇺🇸 English](usuarios.en.md) · [🇯🇵 日本語](usuarios.ja.md) · [🇩🇪 Deutsch](usuarios.de.md) · [🇫🇷 Français](usuarios.fr.md)

# 🎧 Guia do jogador

Este guia é para quem **vai usar** o FFBL no jogo. Quem vai montar a sala no Discord deve ler o [Guia do dono do canal](donos.md).

## O que você precisa

- Final Fantasy XIV com o **XIVLauncher/Dalamud** instalado.
- O **Discord** aberto no mesmo computador (o aplicativo, não só o navegador).
- Três dados que o **dono do canal** vai te enviar em mensagem privada:
  1. **Client ID**
  2. **Client Secret**
  3. **Link do webhook** (trate como uma senha!)
- Para ver o **telão** (opcional): o plugin **Penumbra** e o minion **Plush Cushion**.

> 🎧 **Só quer escutar a música?** Não precisa instalar nada: entre na sala de voz do Discord e pronto.

## Passo 1: aceitar o convite de testador

O dono do canal vai te adicionar como **testador do app**. No Discord, você recebe um convite: **aceite**. Sem isso, o Discord não deixa o plugin se conectar.

## Passo 2: instalar o plugin

1. No jogo, digite `/xlsettings`.
2. Vá em **Experimental**.
3. Em **Repositórios de plugins personalizados**, cole o endereço abaixo, clique em **+** e depois em **Salvar**:

```
https://raw.githubusercontent.com/yurammaruy/FFBL/main/repo.json
```

4. Digite `/xlplugins`, procure por **FFBL** em *Todos os plugins* e clique em **Instalar**.

## Passo 3: conectar

1. Digite `/ffbl` para abrir o iPod.
2. Na tela de conexão, preencha **Client ID**, **Client Secret** e **Webhook**.
3. Clique em **Conectar**.
4. Olhe a janela do **Discord** e clique em **Autorizar**.
5. Quando aparecer *Conectado como (seu nome)*, aperte **MENU** na roda para abrir a playlist.

## Como usar

| Botão da roda | O que faz |
|---|---|
| **MENU** | Alterna entre a tela de conexão e a playlist |
| ⏮ | Volta a música anterior |
| ⏭ | Pula a música |
| ⏯ | Toca ou pausa |
| **Botão do meio** | Abre o campo para pedir música (nome ou link do YouTube) |
| **+ / −** | Volume |
| Ícone no canto inferior | Fecha a janela (o mesmo que `/ffbl`) |

Na aba **Idioma** você troca o idioma do plugin.

## Telão (opcional)

1. Instale o **Penumbra** e deixe-o ligado.
2. Na aba **Projeção**, clique em **Criar telão aqui**. O FFBL invoca o seu *Plush Cushion* e ele vira a tela.
3. Escolha o que mostrar: imagem padrão, capa da música ou uma imagem do seu computador.
4. Ajuste tamanho, posição e giro com os botões **−** e **+**.

Quem não tem o FFBL continua vendo o minion normal.

## OMNE (voz por proximidade): em teste

Está na aba **OMNE**, **desligado por padrão**. Leia a explicação no [início da página](../README.md). Ligue só se o grupo for testar junto.

## Problemas comuns

| Sintoma | O que fazer |
|---|---|
| "Procurando o Discord..." não sai disso | Abra o aplicativo do Discord (não só o navegador) e tente de novo. |
| Não aparece o botão de autorizar | Confira se você aceitou o convite de testador. |
| "O link do webhook está vazio ou inválido" | Cole o link inteiro, que começa com `https://discord.com/api/webhooks/`. |
| O telão não aparece | Confira se o Penumbra está instalado e ligado. |
| A música não toca | Peça ao dono do canal para verificar se o bot está ligado. |

Se nada resolver, abra uma **Issue** aqui no GitHub com um print.
