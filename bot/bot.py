# JBLEZA - bot de musica para Discord (etapa 1: YouTube, fila, comandos de barra)
import asyncio
import getpass
import json
import logging
import os
import random
import re
import shutil
import subprocess
import sys
import time
from collections import deque
from dataclasses import dataclass
from typing import Optional

import discord
from aiohttp import web, WSMsgType
from discord import app_commands
import yt_dlp

PASTA = os.path.dirname(os.path.abspath(__file__))
ARQ_TOKEN = os.path.join(PASTA, "token.txt")
ARQ_LOG = os.path.join(PASTA, "erros.log")
ARQ_CONFIG = os.path.join(PASTA, "config.json")

# Sala do chat (editavel no config.json ou na pagina de teste) e porta local
# Quando aberto pelo painel (FFBL_Painel.pyw) nao ha console para perguntas: nunca espera teclado.
SEM_PERGUNTAS = bool(os.environ.get("FFBL_SEM_PERGUNTAS"))
if SEM_PERGUNTAS:
    import builtins
    builtins.input = lambda *a, **k: ""

CONFIG_PADRAO = {"canal_chat_id": "", "porta": 8765, "atraso_inicio": 6}
LIMITE_TEXTO = 500
PREFIXO_CHAT = "!"  # comandos escritos no chat da sala, ex.: !tocar nome da musica          # letras por mensagem enviada pelo chat
RE_PONTE = re.compile(r"^\*\*(.{1,40}?):\*\* (.*)$", re.DOTALL)  # "**Nome:** texto"

TEMPO_SALA_VAZIA = 180      # segundos sozinho na sala ate o bot parar (3 min)
MAX_FILA = 200              # maximo de musicas esperando na fila
MAX_PLAYLIST = 50           # maximo de musicas ao colar uma playlist
# Ver canais, enviar mensagens, ler o historico, conectar e falar
PERMISSOES_CONVITE = 3222528  # 3214336 + Gerenciar mensagens (para apagar os comandos do chat)

# Silencio antes de cada musica (segundos). Os primeiros segundos de cada musica estavam se perdendo
# no caminho ate quem ouve; esse intervalo de silencio "aquece" a conexao e protege o comeco da musica.
# Pode ser mudado no config.json (chave "atraso_inicio"); 0 desliga.
ATRASO_INICIO_PADRAO = 6.0
FFMPEG_ANTES = "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5"


# ---------------------------------------------------------------- utilidades

def ler_token() -> str:
    if os.path.exists(ARQ_TOKEN):
        with open(ARQ_TOKEN, "r", encoding="utf-8") as f:
            token = f.read().strip()
        if token:
            return token
    print()
    print("=== Primeira vez: falta o token do bot ===")
    print("Cole o token que voce copiou no portal do Discord")
    print("(clique com o botao direito na janela para colar) e aperte Enter.")
    print("O texto NAO aparece enquanto voce cola. Isso e normal e e para a sua seguranca.")
    print()
    token = getpass.getpass("Token (cole e aperte Enter): ").strip().strip('"')
    if not token:
        print("Nenhum token informado. Feche e abra o INICIAR.bat de novo.")
        input("Aperte Enter para sair.")
        sys.exit(1)
    with open(ARQ_TOKEN, "w", encoding="utf-8") as f:
        f.write(token)
    return token


def fmt_duracao(seg) -> str:
    if not seg:
        return "ao vivo"
    seg = int(seg)
    h, resto = divmod(seg, 3600)
    m, s = divmod(resto, 60)
    return f"{h}:{m:02d}:{s:02d}" if h else f"{m}:{s:02d}"


def curto(texto: str, limite: int = 80) -> str:
    texto = discord.utils.escape_markdown(texto or "?")
    return texto if len(texto) <= limite else texto[: limite - 1] + "…"


@dataclass
class Musica:
    url: str
    titulo: str
    duracao: Optional[int]
    pedido_por: str

    @property
    def thumb(self) -> str:
        """Capa da musica (miniatura do YouTube), ou vazio se nao der para achar."""
        m = re.search(r"(?:v=|youtu\.be/|/shorts/|/embed/)([A-Za-z0-9_-]{11})", self.url or "")
        return f"https://i.ytimg.com/vi/{m.group(1)}/mqdefault.jpg" if m else ""


def plano(texto: str, limite: int = 80) -> str:
    """Texto sem formatacao do Discord (para o painel que o plugin le)."""
    texto = " ".join((texto or "?").split())
    return texto if len(texto) <= limite else texto[: limite - 1] + "…"


# ---------------------------------------------------------------- YouTube

def _buscar(consulta: str):
    """Procura no YouTube (nome ou link). Devolve lista de dicts. Bloqueante."""
    eh_url = consulta.lower().startswith(("http://", "https://"))
    alvo = consulta if eh_url else f"ytsearch1:{consulta}"
    quer_lista = eh_url and "/playlist" in consulta
    opts = {
        "quiet": True,
        "no_warnings": True,
        "skip_download": True,
        "extract_flat": "in_playlist",
        "noplaylist": not quer_lista,
        "playlistend": MAX_PLAYLIST,
        "ignore_no_formats_error": True,
        "format": "bestaudio/best",
    }
    with yt_dlp.YoutubeDL(opts) as ydl:
        info = ydl.extract_info(alvo, download=False)
    if not info:
        return []
    entradas = info.get("entries") or [info]
    itens = []
    for e in entradas:
        if not e:
            continue
        url = e.get("webpage_url") or e.get("url") or ""
        if not url.startswith("http") and e.get("id"):
            url = f"https://www.youtube.com/watch?v={e['id']}"
        if not url.startswith("http"):
            continue
        itens.append(
            {
                "url": url,
                "titulo": e.get("title") or "Sem titulo",
                "duracao": e.get("duration"),
            }
        )
    return itens


def escrever_log(texto: str):
    try:
        with open(ARQ_LOG, "a", encoding="utf-8") as f:
            f.write(texto + "\n")
    except OSError:
        pass


def ler_fim_log(linhas: int = 8) -> str:
    try:
        with open(ARQ_LOG, "r", encoding="utf-8", errors="replace") as f:
            conteudo = [l.rstrip() for l in f.readlines() if l.strip()]
        return "\n".join(conteudo[-linhas:])
    except OSError:
        return ""


class _Contador:
    """Conta quantos bytes o YouTube entregou (para saber se o audio chegou)."""

    def __init__(self, fluxo):
        self.fluxo = fluxo
        self.bytes = 0

    def read(self, n=-1):
        dados = self.fluxo.read(n)
        self.bytes += len(dados)
        return dados

    def close(self):
        try:
            self.fluxo.close()
        except Exception:
            pass


class FonteYoutube(discord.FFmpegPCMAudio):
    """O yt-dlp baixa o audio e entrega direto para o FFmpeg. E mais estavel do
    que passar o link do YouTube para o FFmpeg, que o YouTube costuma recusar."""

    def __init__(self, url: str):
        self._yt = None
        self._pre = deque()  # primeiros quadros de audio, lidos antes de comecar a tocar
        try:
            atraso = float(ler_config().get("atraso_inicio", ATRASO_INICIO_PADRAO))
        except Exception:
            atraso = ATRASO_INICIO_PADRAO
        self._silencio = max(0, int(atraso * 50))  # 50 quadros de 20 ms por segundo
        self.contador = None
        self.codigo_yt = None  # codigo de saida do yt-dlp (0 = ok, None = ainda rodando)
        self._log = open(ARQ_LOG, "ab")
        cmd = [
            sys.executable, "-m", "yt_dlp",
            "-f", "bestaudio[ext=webm]/bestaudio/best",
            "--no-playlist", "--no-progress", "-q",
            "-o", "-", url,
        ]
        flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        self._yt = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=self._log, creationflags=flags)
        self.contador = _Contador(self._yt.stdout)
        try:
            super().__init__(
                self.contador,
                pipe=True,
                executable=shutil.which("ffmpeg") or "ffmpeg",
                options="-vn",
                stderr=self._log,
            )
        except Exception:
            self._fechar()
            raise

    def preparar(self, quadros: int = 100):
        """Espera o yt-dlp/FFmpeg entregarem os primeiros ~2 s de audio ANTES de comecar a tocar.
        Sem isso o player do discord.py fica esperando o audio chegar e depois manda tudo
        de uma vez (em rajada): o Discord descarta esses pacotes e os primeiros segundos somem."""
        while len(self._pre) < quadros:
            dados = super().read()
            if not dados:
                break
            self._pre.append(dados)

    def read(self):
        if self._silencio > 0:
            self._silencio -= 1
            return bytes(3840)  # 20 ms de silencio (48 kHz, estereo, 16 bits)
        if self._pre:
            return self._pre.popleft()
        return super().read()

    def _fechar(self):
        if self._yt is not None:
            try:
                self.codigo_yt = self._yt.poll()
            except Exception:
                pass
            try:
                self._yt.kill()
            except Exception:
                pass
            self._yt = None
        if self._log is not None:
            try:
                self._log.close()
            except Exception:
                pass
            self._log = None

    def cleanup(self):
        try:
            super().cleanup()
        finally:
            self._fechar()


# ---------------------------------------------------------------- tocador

class Tocador:
    """Fila e reproducao de UM servidor."""

    def __init__(self, guild: discord.Guild):
        self.guild = guild
        self.fila: deque = deque()
        self.atual: Optional[Musica] = None
        self.canal_texto = None
        self.tarefa_vazia: Optional[asyncio.Task] = None
        self.iniciando = False
        self.geracao = 0  # sobe a cada "parar", para descartar musicas em preparo
        self.ultimo_pulo = 0.0  # hora do ultimo /pular (para nao confundir com falha)
        self.anterior: Optional[Musica] = None  # ultima musica que ja terminou/foi pulada (botao voltar)
        self.volume = 100  # porcentagem (0 a 150), vale para as proximas musicas e para a que esta tocando
        self.telao: Optional[str] = None  # telao no mundo do jogo (texto "territorio | x | y | z | largura | altura | giro | dono")
        self.video: Optional[str] = None  # video exibido no telao ("inicio_epoch | url | quem")
        self.pausado_video = False  # a musica foi pausada por causa do video
        self.musica_inicio = 0.0  # hora (epoch) em que a musica atual comecou
        self.painel_msg = None  # mensagem unica de "painel" que o plugin do jogo le
        self._painel_tarefa: Optional[asyncio.Task] = None
        self._painel_pendente = False

    @property
    def vc(self) -> Optional[discord.VoiceClient]:
        return self.guild.voice_client

    # ---- painel (mensagem unica, editada a cada mudanca; o plugin FFBL le isto) ----
    def pedir_painel(self):
        if self.canal_texto is None:
            return
        if self._painel_tarefa is not None and not self._painel_tarefa.done():
            self._painel_pendente = True
            return
        self._painel_tarefa = asyncio.get_running_loop().create_task(self._painel_loop())

    async def _painel_loop(self):
        while True:
            self._painel_pendente = False
            try:
                await self._atualizar_painel()
            except Exception as e:
                print(f"[erro] Nao consegui atualizar o painel: {e}")
            if not self._painel_pendente:
                break
            await asyncio.sleep(0.3)

    async def _atualizar_painel(self):
        m = self.atual
        vc = self.vc
        if m is None:
            estado, titulo = "Parado", "Nada tocando"
        else:
            estado = "Pausado" if (vc is not None and vc.is_paused()) else "Tocando agora"
            titulo = plano(m.titulo, 200)
        linhas = []
        proximas = list(self.fila)
        for i, x in enumerate(proximas[:10], 1):
            linhas.append(f"{i}. {plano(x.titulo, 90)} · pedido de {plano(x.pedido_por, 20)}")
        if len(proximas) > 10:
            linhas.append(f"... e mais {len(proximas) - 10} na fila")
        emb = discord.Embed(
            title=titulo,
            url=(m.url if m is not None and m.url.startswith("http") else None),
            description="\n".join(linhas) if linhas else None,
            color=0x2B6CB0,
        )
        emb.set_author(name=estado)
        if m is not None and m.thumb:
            emb.set_thumbnail(url=m.thumb)
        emb.add_field(name="volume", value=str(self.volume), inline=True)
        emb.add_field(name="telao", value=self.telao or "nenhum", inline=True)
        try:
            emb.add_field(name="botid", value=str(self.guild.me.id), inline=True)  # o plugin usa para o volume pessoal
        except Exception:
            pass
        if self.video:
            emb.add_field(name="video", value=self.video[:1000], inline=False)
        if m is not None and self.musica_inicio:
            emb.add_field(name="inicio", value=f"{self.musica_inicio:.0f}", inline=True)
        emb.set_footer(text="Painel JBLEZA")
        if self.painel_msg is not None:
            try:
                await self.painel_msg.edit(embed=emb)
                return
            except Exception:
                self.painel_msg = None
        self.painel_msg = await self.canal_texto.send(embed=emb)

    def aplicar_volume(self):
        vc = self.vc
        src = vc.source if vc is not None else None
        if isinstance(src, discord.PCMVolumeTransformer):
            src.volume = self.volume / 100

    async def avisar(self, texto: str):
        if self.canal_texto is None:
            return
        try:
            await self.canal_texto.send(texto)
        except Exception:
            pass

    def ocupado(self) -> bool:
        vc = self.vc
        return bool(vc and (vc.is_playing() or vc.is_paused())) or self.iniciando

    async def tocar_proxima(self):
        if self.iniciando:
            return
        self.iniciando = True
        try:
            while True:
                vc = self.vc
                if vc is None or not vc.is_connected():
                    self.atual = None
                    return
                if vc.is_playing() or vc.is_paused():
                    return
                if not self.fila:
                    self.atual = None
                    return
                musica = self.fila.popleft()
                self.atual = musica
                geracao = self.geracao
                loop = asyncio.get_running_loop()
                escrever_log(f"--- {time.strftime('%H:%M:%S')} tocando: {musica.titulo} ({musica.url})")
                try:
                    fonte = FonteYoutube(musica.url)
                except Exception as e:
                    print(f"[erro] Nao consegui abrir '{musica.titulo}': {e}")
                    await self.avisar(f"Nao consegui tocar **{curto(musica.titulo)}**. Pulando para a proxima.")
                    self.atual = None
                    continue
                # aquece o audio (os primeiros segundos chegavam atrasados e eram perdidos)
                try:
                    await asyncio.wait_for(loop.run_in_executor(None, fonte.preparar), timeout=45)
                except Exception as e:
                    escrever_log(f"aviso: nao consegui aquecer o audio de '{musica.titulo}': {e}")
                if geracao != self.geracao or vc is None or not vc.is_connected():
                    fonte.cleanup()
                    self.atual = None
                    return
                if vc.is_playing() or vc.is_paused():
                    fonte.cleanup()
                    return
                inicio = time.monotonic()

                def depois(erro, loop=loop, musica=musica, inicio=inicio, geracao=geracao, fonte=fonte):
                    asyncio.run_coroutine_threadsafe(self._depois(erro, musica, inicio, geracao, fonte), loop)

                vc.play(discord.PCMVolumeTransformer(fonte, volume=self.volume / 100), after=depois)
                self.musica_inicio = time.time()
                print(f"[acao] Tocando: {musica.titulo} (pedido por {musica.pedido_por})")
                await self.avisar(
                    f"Tocando agora: **{curto(musica.titulo)}** [{fmt_duracao(musica.duracao)}] "
                    f"- pedido por {curto(musica.pedido_por, 30)}"
                )
                self.pedir_painel()
                return
        finally:
            self.iniciando = False

    async def _depois(self, erro, musica=None, inicio=None, geracao=None, fonte=None):
        if erro:
            print(f"[erro] Falha durante a reproducao: {erro}")
        recebidos = 0
        if fonte is not None and fonte.contador is not None:
            recebidos = fonte.contador.bytes
        if musica is not None and fonte is not None:
            decorrido = time.monotonic() - inicio if inicio is not None else 0
            escrever_log(
                f"[audio] '{musica.titulo}': {recebidos} bytes recebidos do YouTube em {decorrido:.0f}s, "
                f"yt-dlp codigo de saida: {fonte.codigo_yt}"
            )
        falhou = False
        if musica is not None and inicio is not None and geracao == self.geracao:
            agora = time.monotonic()
            rapido = agora - inicio < 6
            poucos = recebidos < 200_000  # uma musica inteira tem alguns milhoes de bytes
            longa = (musica.duracao or 60) > 20
            pulou = agora - self.ultimo_pulo < 3
            falhou = (rapido or poucos) and longa and not pulou
        self.atual = None
        if musica is not None:
            self.anterior = musica
        if falhou and musica is not None:
            detalhe = ler_fim_log()
            print()
            print(f"[erro] A musica '{musica.titulo}' nao chegou direito ({recebidos} bytes). Ultimas linhas do erros.log:")
            print(detalhe or "(sem detalhes)")
            print()
            await self.avisar(
                f"**{curto(musica.titulo)}** nao chegou direito do YouTube ({recebidos} bytes). "
                "Veja a janela do bot."
            )
        await self.tocar_proxima()
        self.pedir_painel()

    async def parar(self):
        self.geracao += 1
        self.fila.clear()
        self.atual = None
        vc = self.vc
        if vc and (vc.is_playing() or vc.is_paused()):
            vc.stop()
        self.pedir_painel()

    async def encerrar(self, motivo: Optional[str] = None):
        await self.parar()
        vc = self.vc
        if vc and vc.is_connected():
            try:
                await vc.disconnect()
            except Exception:
                pass
        self._cancelar_timer()
        if motivo:
            await self.avisar(motivo)

    def limpar(self):
        """Chamado quando o bot saiu da sala (ex.: foi desconectado)."""
        self.geracao += 1
        self.fila.clear()
        self.atual = None
        self._cancelar_timer()

    # --- sala vazia

    def _cancelar_timer(self):
        if self.tarefa_vazia and not self.tarefa_vazia.done():
            self.tarefa_vazia.cancel()
        self.tarefa_vazia = None

    def verificar_sala(self):
        vc = self.vc
        if vc is None or not vc.is_connected():
            self._cancelar_timer()
            return
        humanos = [m for m in vc.channel.members if not m.bot]
        if humanos:
            self._cancelar_timer()
        elif self.tarefa_vazia is None or self.tarefa_vazia.done():
            self.tarefa_vazia = asyncio.ensure_future(self._esperar_sala_vazia())

    async def _esperar_sala_vazia(self):
        try:
            await asyncio.sleep(TEMPO_SALA_VAZIA)
        except asyncio.CancelledError:
            return
        await self.encerrar("A sala ficou vazia por 3 minutos. Parei a musica e saí da sala.")


# ---------------------------------------------------------------- bot

# ---------------------------------------------------------------- chat

def ler_config() -> dict:
    cfg = dict(CONFIG_PADRAO)
    try:
        with open(ARQ_CONFIG, "r", encoding="utf-8") as f:
            lido = json.load(f)
        if isinstance(lido, dict):
            cfg.update(lido)
    except FileNotFoundError:
        salvar_config(cfg)
    except Exception as e:
        print(f"[aviso] Nao consegui ler o config.json ({e}). Usando os valores padrao.")
    return cfg


def salvar_config(cfg: dict):
    try:
        with open(ARQ_CONFIG, "w", encoding="utf-8") as f:
            json.dump(cfg, f, indent=2, ensure_ascii=False)
    except OSError as e:
        print(f"[aviso] Nao consegui salvar o config.json: {e}")


def link_de_video_ok(alvo: str) -> bool:
    """So aceita http/https, sem espacos e sem enderecos do proprio computador ou da rede local."""
    if not alvo or len(alvo) > 900 or re.search(r"\s", alvo):
        return False
    m = re.match(r"^https?://([^/:?#]+)", alvo, re.IGNORECASE)
    if not m:
        return False
    host = m.group(1).lower()
    if host in ("localhost",) or host.endswith(".local") or host.endswith(".internal"):
        return False
    if re.match(r"^(127\.|10\.|192\.168\.|169\.254\.|172\.(1[6-9]|2\d|3[01])\.|0\.)", host) or host in ("::1",):
        return False
    return True


def limpar_nome(nome: str) -> str:
    nome = re.sub(r"[\*_~`|>:\r\n\t]", "", nome).strip()
    return nome[:32] or "Alguem"


PAGINA_HTML = r"""<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>JBLEZA - Chat de teste</title>
<style>
:root{--bg:#12121a;--panel:#1b1b27;--line:#2c2c3d;--txt:#ececf4;--mut:#9a9ab0;--acc:#8b6cff;--ok:#3ecf8e;--off:#ff6b6b;--bot:#e8b84b}
*{box-sizing:border-box}
html,body{height:100%}
body{margin:0;background:var(--bg);color:var(--txt);font:15px/1.4 system-ui,"Segoe UI",sans-serif;display:flex;justify-content:center}
main{width:100%;max-width:560px;height:100%;display:flex;flex-direction:column;padding:12px 16px;gap:10px}
header{display:flex;align-items:center;gap:10px}
h1{font-size:18px;margin:0;flex:1}
.ponto{width:10px;height:10px;border-radius:50%;background:var(--off)}
.ponto.ok{background:var(--ok)}
#sala{color:var(--mut);font-size:13px}
.linha{display:flex;gap:8px}
label{font-size:12px;color:var(--mut);display:block;margin-bottom:2px}
input,button{font:inherit;color:var(--txt);background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:8px 10px}
input{flex:1;min-width:0}
input:focus{outline:2px solid var(--acc);outline-offset:0}
button{cursor:pointer;background:var(--acc);border-color:var(--acc);color:#fff;font-weight:600}
button.sec{background:var(--panel);border-color:var(--line);color:var(--txt)}
#aviso{display:none;background:#3a1d24;border:1px solid var(--off);color:#ffd2d2;border-radius:8px;padding:8px 10px;font-size:14px}
#lista{flex:1;overflow-y:auto;background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:10px;display:flex;flex-direction:column;gap:8px}
.msg .cab{font-size:12px;color:var(--mut)}
.msg .cab b{color:var(--txt);font-size:13px}
.msg.bot .cab b{color:var(--bot)}
.msg .txt{white-space:pre-wrap;word-break:break-word}
</style>
</head>
<body>
<main>
<header>
<span id="ponto" class="ponto"></span>
<h1>JBLEZA - Chat da sala</h1>
<span id="sala">conectando...</span>
</header>
<div id="aviso"></div>
<div class="linha">
<div style="flex:1"><label for="nome">Seu nome no chat</label><input id="nome" maxlength="32" placeholder="Ex.: Lirou"></div>
</div>
<div class="linha">
<div style="flex:1"><label for="idsala">ID da sala (voz ou texto)</label><input id="idsala" inputmode="numeric" placeholder="Cole o ID da sala"></div>
<div style="align-self:flex-end"><button id="trocar" class="sec" type="button">Trocar sala</button></div>
</div>
<div id="lista" aria-live="polite"></div>
<div class="linha">
<input id="texto" maxlength="500" placeholder="Escreva e aperte Enter" autocomplete="off">
<button id="enviar" type="button">Enviar</button>
</div>
</main>
<script>
const $ = id => document.getElementById(id);
let ws = null;
let timerAviso = null;

function guardar(k, v) { try { localStorage.setItem(k, v); } catch (e) {} }
function ler(k) { try { return localStorage.getItem(k) || ""; } catch (e) { return ""; } }
$("nome").value = ler("nome");

function status(ok, txt) {
  $("ponto").className = "ponto" + (ok ? " ok" : "");
  $("sala").textContent = txt;
}
function aviso(t) {
  const a = $("aviso");
  if (timerAviso) { clearTimeout(timerAviso); timerAviso = null; }
  a.textContent = t || "";
  a.style.display = t ? "block" : "none";
  if (t) { timerAviso = setTimeout(() => { a.style.display = "none"; }, 8000); }
}
function adicionar(m) {
  const l = $("lista");
  const perto = l.scrollHeight - l.scrollTop - l.clientHeight < 80;
  const d = document.createElement("div");
  d.className = "msg" + (m.bot ? " bot" : "");
  const c = document.createElement("div");
  c.className = "cab";
  const a = document.createElement("b");
  a.textContent = m.autor;
  const h = document.createElement("span");
  h.textContent = " " + m.hora;
  c.append(a, h);
  const t = document.createElement("div");
  t.className = "txt";
  t.textContent = m.texto;
  d.append(c, t);
  l.append(d);
  while (l.children.length > 300) { l.removeChild(l.firstChild); }
  if (perto) { l.scrollTop = l.scrollHeight; }
}
function conectar() {
  ws = new WebSocket("ws://" + location.host + "/ws");
  ws.onclose = () => { status(false, "sem conexao com o bot"); setTimeout(conectar, 2000); };
  ws.onmessage = (e) => {
    let d;
    try { d = JSON.parse(e.data); } catch (x) { return; }
    if (d.tipo === "estado") {
      if (document.activeElement !== $("idsala")) { $("idsala").value = d.id || ""; }
      if (d.ok) { status(true, d.canal + (d.servidor ? " (" + d.servidor + ")" : "")); aviso(""); }
      else { status(false, "sala nao encontrada"); aviso(d.erro); }
    } else if (d.tipo === "limpar") {
      $("lista").textContent = "";
    } else if (d.tipo === "msg") {
      adicionar(d);
    } else if (d.tipo === "erro") {
      aviso(d.texto);
    }
  };
}
function enviar() {
  const t = $("texto").value.trim();
  if (!t || !ws || ws.readyState !== 1) { return; }
  const n = $("nome").value.trim() || "Alguem";
  guardar("nome", n);
  ws.send(JSON.stringify({ tipo: "enviar", texto: t, nome: n }));
  $("texto").value = "";
}
function trocar() {
  if (!ws || ws.readyState !== 1) { return; }
  ws.send(JSON.stringify({ tipo: "canal", id: $("idsala").value.trim() }));
}
$("enviar").addEventListener("click", enviar);
$("trocar").addEventListener("click", trocar);
$("texto").addEventListener("keydown", (e) => { if (e.key === "Enter") { enviar(); } });
$("idsala").addEventListener("keydown", (e) => { if (e.key === "Enter") { trocar(); } });
conectar();
</script>
</body>
</html>
"""


class PonteChat:
    """Liga o chat de uma sala do Discord a janelas do proprio computador
    (a pagina de teste agora, o plugin do FF14 depois). So aceita conexoes locais."""

    def __init__(self, bot):
        self.bot = bot
        self.cfg = ler_config()
        self.canal = None
        self.clientes = set()
        self.envios = {}
        self.runner = None
        self.ativo = False

    @property
    def porta(self) -> int:
        try:
            return int(self.cfg.get("porta", 8765))
        except (TypeError, ValueError):
            return 8765

    async def iniciar(self):
        app = web.Application()
        app.router.add_get("/", self.pagina)
        app.router.add_get("/ws", self.websocket)
        self.runner = web.AppRunner(app)
        await self.runner.setup()
        try:
            site = web.TCPSite(self.runner, "127.0.0.1", self.porta)
            await site.start()
            self.ativo = True
        except OSError as e:
            print(f"[erro] Nao consegui abrir o chat na porta {self.porta}: {e}")
            print("       Feche outro JBLEZA que esteja aberto ou mude a porta no config.json.")
            await self.runner.cleanup()
            self.runner = None

    async def parar(self):
        for ws in list(self.clientes):
            try:
                await ws.close()
            except Exception:
                pass
        if self.runner is not None:
            try:
                await self.runner.cleanup()
            except Exception:
                pass
            self.runner = None

    def _origem_ok(self, request) -> bool:
        origem = request.headers.get("Origin")
        if origem is None:
            return True  # programas (como o plugin) nao mandam Origin; navegadores mandam
        return origem in (f"http://127.0.0.1:{self.porta}", f"http://localhost:{self.porta}")

    async def pagina(self, request):
        return web.Response(text=PAGINA_HTML, content_type="text/html", charset="utf-8")

    # --- sala

    async def carregar_canal(self):
        """Devolve None se deu certo, ou o texto do erro."""
        self.canal = None
        cid = str(self.cfg.get("canal_chat_id", "")).strip()
        if not cid.isdigit():
            return "O ID da sala precisa ser so numeros."
        try:
            c = self.bot.get_channel(int(cid)) or await self.bot.fetch_channel(int(cid))
        except discord.NotFound:
            return "Nao achei essa sala. Confira o ID e se o JBLEZA esta nesse servidor."
        except discord.Forbidden:
            return "O JBLEZA nao tem acesso a essa sala."
        except Exception as e:
            return f"Nao consegui abrir a sala: {e}"
        if not hasattr(c, "send") or not hasattr(c, "history"):
            return "Esse ID nao e de uma sala com chat de texto."
        self.canal = c
        return None

    async def garantir_canal(self):
        if self.canal is not None:
            return None
        return await self.carregar_canal()

    # --- mensagens

    def formatar(self, m):
        autor = m.author.display_name
        texto = (m.clean_content or "").strip()
        eh_bot = m.author.bot
        if m.author.id == self.bot.user.id:
            achou = RE_PONTE.match(m.content or "")
            if achou:  # mensagem enviada por uma pessoa atraves do bot
                autor, texto, eh_bot = achou.group(1), achou.group(2).strip(), False
        if not texto and m.embeds:
            e = m.embeds[0]
            texto = (e.description or e.title or "").strip()
        if not texto and m.attachments:
            texto = "[anexo]"
        if not texto:
            return None
        return {
            "tipo": "msg",
            "autor": autor[:40],
            "texto": texto[:600],
            "hora": m.created_at.astimezone().strftime("%H:%M"),
            "bot": eh_bot,
        }

    async def historico(self):
        itens = []
        try:
            async for m in self.canal.history(limit=15):
                item = self.formatar(m)
                if item:
                    itens.append(item)
        except (discord.Forbidden, discord.HTTPException):
            return []  # sem permissao de ler o historico: comeca vazio
        itens.reverse()
        return itens

    async def _enviar(self, ws, item) -> bool:
        try:
            await ws.send_str(json.dumps(item, ensure_ascii=False))
            return True
        except Exception:
            self.clientes.discard(ws)
            return False

    async def transmitir(self, item):
        for ws in list(self.clientes):
            await self._enviar(ws, item)

    async def enviar_estado(self, ws):
        erro = await self.garantir_canal()
        if erro:
            await self._enviar(ws, {"tipo": "estado", "ok": False, "erro": erro,
                                    "id": str(self.cfg.get("canal_chat_id", ""))})
            return
        c = self.canal
        servidor = getattr(getattr(c, "guild", None), "name", "")
        await self._enviar(ws, {"tipo": "estado", "ok": True, "canal": getattr(c, "name", "?"),
                                "servidor": servidor, "id": str(c.id)})
        await self._enviar(ws, {"tipo": "limpar"})
        for item in await self.historico():
            await self._enviar(ws, item)

    # --- conexoes locais

    async def websocket(self, request):
        if not self._origem_ok(request):
            return web.Response(status=403, text="origem nao permitida")
        ws = web.WebSocketResponse(heartbeat=30, max_msg_size=16384)
        await ws.prepare(request)
        self.clientes.add(ws)
        try:
            await self.bot.wait_until_ready()
            await self.enviar_estado(ws)
            async for msg in ws:
                if msg.type != WSMsgType.TEXT:
                    continue
                try:
                    dados = json.loads(msg.data)
                except ValueError:
                    continue
                if isinstance(dados, dict):
                    await self.tratar(ws, dados)
        except Exception as e:
            print(f"[erro] Chat local: {e!r}")
        finally:
            self.clientes.discard(ws)
            self.envios.pop(ws, None)
        return ws

    async def tratar(self, ws, dados: dict):
        tipo = dados.get("tipo")
        if tipo == "enviar":
            await self.tratar_envio(ws, dados)
        elif tipo == "canal":
            await self.tratar_troca(ws, dados)

    async def tratar_envio(self, ws, dados: dict):
        texto = str(dados.get("texto", "")).strip()
        if not texto:
            return
        if len(texto) > LIMITE_TEXTO:
            await self._enviar(ws, {"tipo": "erro", "texto": f"Mensagem muito longa (maximo {LIMITE_TEXTO} letras)."})
            return
        agora = time.monotonic()
        recentes = [t for t in self.envios.get(ws, []) if agora - t < 10]
        if len(recentes) >= 5:
            self.envios[ws] = recentes
            await self._enviar(ws, {"tipo": "erro", "texto": "Devagar: no maximo 5 mensagens a cada 10 segundos."})
            return
        recentes.append(agora)
        self.envios[ws] = recentes
        erro = await self.garantir_canal()
        if erro:
            await self._enviar(ws, {"tipo": "erro", "texto": erro})
            return
        nome = limpar_nome(str(dados.get("nome", "")))
        try:
            await self.canal.send(f"**{nome}:** {texto}")
        except discord.Forbidden:
            await self._enviar(ws, {"tipo": "erro",
                                    "texto": "O JBLEZA nao pode escrever nessa sala. De a ele a permissao 'Enviar mensagens'."})
            return
        except discord.HTTPException as e:
            await self._enviar(ws, {"tipo": "erro", "texto": f"O Discord recusou a mensagem: {e}"})
            return
        await self.transmitir({"tipo": "msg", "autor": nome, "texto": texto,
                               "hora": time.strftime("%H:%M"), "bot": False})

    async def trocar_sala(self, novo: str):
        """Usado pela janela: troca a sala ativa. Devolve None se deu certo, ou o texto do erro."""
        novo = str(novo).strip()
        if not novo.isdigit():
            return "O ID da sala precisa ser so numeros."
        antigo = self.cfg.get("canal_chat_id")
        self.cfg["canal_chat_id"] = novo
        erro = await self.carregar_canal()
        if erro:
            self.cfg["canal_chat_id"] = antigo
            await self.carregar_canal()
            return erro
        salvar_config(self.cfg)
        for c in list(self.clientes):
            await self.enviar_estado(c)
        return None

    async def tratar_troca(self, ws, dados: dict):
        novo = str(dados.get("id", "")).strip()
        if not novo.isdigit():
            await self._enviar(ws, {"tipo": "erro", "texto": "O ID da sala precisa ser so numeros."})
            return
        antigo = self.cfg.get("canal_chat_id")
        self.cfg["canal_chat_id"] = novo
        erro = await self.carregar_canal()
        if erro:
            self.cfg["canal_chat_id"] = antigo
            await self.carregar_canal()
            await self._enviar(ws, {"tipo": "erro", "texto": erro})
            return
        salvar_config(self.cfg)
        for c in list(self.clientes):
            await self.enviar_estado(c)


class Jbleza(discord.Client):
    def __init__(self):
        intents = discord.Intents.default()  # inclui guilds e estados de voz
        intents.message_content = True  # ler o texto do chat (ligar no portal do Discord)
        super().__init__(intents=intents, allowed_mentions=discord.AllowedMentions.none())
        self.tree = app_commands.CommandTree(self)
        self.tocadores: dict = {}
        self.ponte = None
        self._pronto = False

    async def setup_hook(self):
        self.ponte = PonteChat(self)
        await self.ponte.iniciar()

    async def close(self):
        if self.ponte is not None:
            await self.ponte.parar()
        await super().close()

    async def on_message(self, message):
        p = self.ponte
        if p is None or p.canal is None or message.channel.id != p.canal.id:
            return
        if message.author.id == self.user.id:
            return  # o que o bot posta pelo chat ja foi transmitido ao enviar
        item = p.formatar(message)
        if item:
            await p.transmitir(item)
        # comandos escritos no chat da sala (o plugin FFBL manda por webhook, ou alguem digita)
        texto = (message.content or "").strip()
        if texto.startswith(PREFIXO_CHAT) and message.guild is not None:
            try:
                await executar_comando_chat(message, texto[len(PREFIXO_CHAT):].strip())
            except Exception as e:
                print(f"[erro] Comando do chat falhou ('{texto[:60]}'): {e}")
                try:
                    await message.channel.send("Deu um erro nesse comando. Veja a janela do bot.")
                except Exception:
                    pass

    def tocador(self, guild: discord.Guild) -> Tocador:
        t = self.tocadores.get(guild.id)
        if t is None:
            t = Tocador(guild)
            self.tocadores[guild.id] = t
        return t

    async def sincronizar(self, guild: discord.Guild):
        try:
            self.tree.copy_global_to(guild=guild)
            await self.tree.sync(guild=guild)
            print(f"[ok] Comandos prontos no servidor: {guild.name}")
        except Exception as e:
            print(f"[erro] Nao consegui registrar os comandos em {guild.name}: {e}")

    async def on_ready(self):
        if self._pronto:
            return
        self._pronto = True
        print()
        print(f"JBLEZA conectado como {self.user}.")
        print("[acao] Bot conectado ao Discord")
        for g in self.guilds:
            await self.sincronizar(g)
        convite = (
            f"https://discord.com/oauth2/authorize?client_id={self.application_id}"
            f"&permissions={PERMISSOES_CONVITE}&scope=bot+applications.commands"
        )
        print()
        print("Link para convidar o JBLEZA para um servidor:")
        print(convite)
        try:
            subprocess.run(["clip"], input=convite, text=True, check=False)
            print("(o link ja foi copiado para a area de transferencia)")
        except Exception:
            pass
        print()
        print("Deixe esta janela ABERTA. Fechar a janela desliga o bot.")
        if self.ponte is not None and self.ponte.ativo:
            erro = await self.ponte.garantir_canal()
            print()
            if erro:
                print(f"[chat] Atencao: {erro}")
            else:
                print(f"[chat] Sala do chat: {getattr(self.ponte.canal, 'name', '?')}")
            print(f"[chat] Para testar, abra no navegador: http://127.0.0.1:{self.ponte.porta}")

    async def on_guild_join(self, guild: discord.Guild):
        await self.sincronizar(guild)

    async def on_voice_state_update(self, member, antes, depois):
        t = self.tocadores.get(member.guild.id)
        if t is None:
            return
        if member.id == self.user.id and depois.channel is None:
            t.limpar()
            return
        t.verificar_sala()


bot = Jbleza()


# ---------------------------------------------------------------- comandos

def _canal_do_usuario(interaction: discord.Interaction):
    m = interaction.user
    if isinstance(m, discord.Member) and m.voice and m.voice.channel:
        return m.voice.channel
    return None


async def _controle(interaction: discord.Interaction) -> Optional[Tocador]:
    t = bot.tocadores.get(interaction.guild_id)
    if t is None or t.vc is None or not t.vc.is_connected():
        await interaction.response.send_message("Nao estou em nenhuma sala de voz. Use /tocar.", ephemeral=True)
        return None
    canal = _canal_do_usuario(interaction)
    if canal is None or canal.id != t.vc.channel.id:
        await interaction.response.send_message(
            "Entre na mesma sala de voz que eu para usar este comando.", ephemeral=True
        )
        return None
    t.canal_texto = interaction.channel
    return t


@bot.tree.command(name="tocar", description="Toca uma musica do YouTube (nome ou link) ou coloca na fila")
@app_commands.describe(busca="Nome da musica ou link do YouTube")
@app_commands.guild_only()
async def tocar(interaction: discord.Interaction, busca: str):
    canal = _canal_do_usuario(interaction)
    if canal is None:
        await interaction.response.send_message("Entre em uma sala de voz primeiro.", ephemeral=True)
        return
    if "spotify.com" in busca.lower():
        await interaction.response.send_message(
            "Links do Spotify ainda nao funcionam (isso chega numa proxima versao). "
            "Por enquanto use o nome da musica ou um link do YouTube.",
            ephemeral=True,
        )
        return

    await interaction.response.defer()
    t = bot.tocador(interaction.guild)
    vc = interaction.guild.voice_client

    if vc and vc.is_connected() and vc.channel.id != canal.id and (t.ocupado() or t.fila):
        await interaction.followup.send("Ja estou tocando em outra sala de voz deste servidor.")
        return

    loop = asyncio.get_running_loop()
    try:
        itens = await loop.run_in_executor(None, _buscar, busca.strip())
    except Exception as e:
        print(f"[erro] Busca falhou ('{busca}'): {e}")
        await interaction.followup.send("Nao consegui achar essa musica. Tente outro nome ou link.")
        return
    if not itens:
        await interaction.followup.send("Nao achei nada com essa busca.")
        return

    espaco = MAX_FILA - len(t.fila)
    if espaco <= 0:
        await interaction.followup.send("A fila esta cheia.")
        return
    itens = itens[:espaco]

    try:
        if vc is None or not vc.is_connected():
            vc = await canal.connect(self_deaf=True)
        elif vc.channel.id != canal.id:
            await vc.move_to(canal)
    except Exception as e:
        print(f"[erro] Nao consegui entrar na sala: {e}")
        await interaction.followup.send(
            "Nao consegui entrar na sala de voz. Confira se o JBLEZA tem as permissoes "
            "'Conectar' e 'Falar' nessa sala."
        )
        return

    t.canal_texto = interaction.channel
    esperando = t.ocupado() or len(t.fila) > 0
    for it in itens:
        t.fila.append(Musica(it["url"], it["titulo"], it["duracao"], interaction.user.display_name))
        print(f"[acao] Na fila: {it['titulo']} (pedido por {interaction.user.display_name})")

    if len(itens) > 1:
        await interaction.followup.send(f"{len(itens)} musicas adicionadas a fila.")
    elif esperando:
        await interaction.followup.send(
            f"Adicionada a fila: **{curto(itens[0]['titulo'])}** [{fmt_duracao(itens[0]['duracao'])}]"
        )
    else:
        await interaction.followup.send(f"Vou tocar: **{curto(itens[0]['titulo'])}**")

    await t.tocar_proxima()
    t.verificar_sala()


@bot.tree.command(name="fila", description="Mostra o que esta tocando e as proximas musicas")
@app_commands.guild_only()
async def fila(interaction: discord.Interaction):
    t = bot.tocadores.get(interaction.guild_id)
    if t is None or (t.atual is None and not t.fila):
        await interaction.response.send_message("A fila esta vazia.")
        return
    linhas = []
    if t.atual:
        linhas.append(
            f"**Tocando agora:** {curto(t.atual.titulo)} [{fmt_duracao(t.atual.duracao)}] "
            f"- pedido por {curto(t.atual.pedido_por, 30)}"
        )
    proximas = list(t.fila)
    for i, m in enumerate(proximas[:10], 1):
        linhas.append(f"{i}. {curto(m.titulo)} [{fmt_duracao(m.duracao)}] - {curto(m.pedido_por, 30)}")
    if len(proximas) > 10:
        linhas.append(f"... e mais {len(proximas) - 10} na fila.")
    await interaction.response.send_message("\n".join(linhas))


@bot.tree.command(name="agora", description="Mostra a musica que esta tocando")
@app_commands.guild_only()
async def agora(interaction: discord.Interaction):
    t = bot.tocadores.get(interaction.guild_id)
    if t is None or t.atual is None:
        await interaction.response.send_message("Nao tem nada tocando agora.")
        return
    m = t.atual
    await interaction.response.send_message(
        f"Tocando agora: **{curto(m.titulo)}** [{fmt_duracao(m.duracao)}] - pedido por {curto(m.pedido_por, 30)}\n{m.url}"
    )


@bot.tree.command(name="pular", description="Pula para a proxima musica")
@app_commands.guild_only()
async def pular(interaction: discord.Interaction):
    t = await _controle(interaction)
    if t is None:
        return
    if t.atual is None or not (t.vc.is_playing() or t.vc.is_paused()):
        await interaction.response.send_message("Nao tem nada tocando para pular.", ephemeral=True)
        return
    titulo = t.atual.titulo
    t.ultimo_pulo = time.monotonic()
    t.vc.stop()
    print(f"[acao] Musica pulada: {titulo}")
    await interaction.response.send_message(f"Pulei: **{curto(titulo)}**")


@bot.tree.command(name="pausar", description="Pausa a musica")
@app_commands.guild_only()
async def pausar(interaction: discord.Interaction):
    t = await _controle(interaction)
    if t is None:
        return
    if t.vc.is_playing():
        t.vc.pause()
        print("[acao] Musica pausada")
        await interaction.response.send_message("Musica pausada. Use /continuar para voltar.")
    else:
        await interaction.response.send_message("Nao tem nada tocando para pausar.", ephemeral=True)


@bot.tree.command(name="continuar", description="Continua a musica pausada")
@app_commands.guild_only()
async def continuar(interaction: discord.Interaction):
    t = await _controle(interaction)
    if t is None:
        return
    if t.vc.is_paused():
        t.vc.resume()
        print("[acao] Musica continuou")
        await interaction.response.send_message("Voltei a tocar.")
    else:
        await interaction.response.send_message("A musica nao esta pausada.", ephemeral=True)


@bot.tree.command(name="parar", description="Para a musica e limpa a fila (o bot continua na sala)")
@app_commands.guild_only()
async def parar(interaction: discord.Interaction):
    t = await _controle(interaction)
    if t is None:
        return
    await t.parar()
    await interaction.response.send_message("Parei a musica e limpei a fila.")


@bot.tree.command(name="sair", description="Para a musica, limpa a fila e sai da sala de voz")
@app_commands.guild_only()
async def sair(interaction: discord.Interaction):
    t = await _controle(interaction)
    if t is None:
        return
    await interaction.response.send_message("Saindo da sala. Ate mais!")
    await t.encerrar()


@bot.tree.command(name="remover", description="Tira uma musica da fila pelo numero (veja em /fila)")
@app_commands.describe(posicao="Numero da musica na fila")
@app_commands.guild_only()
async def remover(interaction: discord.Interaction, posicao: app_commands.Range[int, 1, 200]):
    t = await _controle(interaction)
    if t is None:
        return
    if posicao > len(t.fila):
        await interaction.response.send_message("Esse numero nao existe na fila.", ephemeral=True)
        return
    lista = list(t.fila)
    removida = lista.pop(posicao - 1)
    t.fila = deque(lista)
    await interaction.response.send_message(f"Removi da fila: **{curto(removida.titulo)}**")


@bot.tree.command(name="embaralhar", description="Embaralha a ordem da fila")
@app_commands.guild_only()
async def embaralhar(interaction: discord.Interaction):
    t = await _controle(interaction)
    if t is None:
        return
    if len(t.fila) < 2:
        await interaction.response.send_message("Precisa ter pelo menos 2 musicas na fila.", ephemeral=True)
        return
    lista = list(t.fila)
    random.shuffle(lista)
    t.fila = deque(lista)
    await interaction.response.send_message("Fila embaralhada.")


# ---------------------------------------------------------------- comandos pelo chat da sala

AJUDA_CHAT = (
    "Comandos: `!tocar nome ou link` | `!fila` | `!agora` | `!pular` | `!pausar` | `!continuar` | "
    "`!parar` | `!sair` | `!remover numero` | `!embaralhar` | `!voltar` (recoloca a ultima musica na fila) | `!volume +` `!volume -` `!volume 80`"
)


_avisou_apagar = False


async def executar_comando_chat(message: discord.Message, linha: str):
    """Obedece um comando escrito no chat da sala de voz. O bot toca na PROPRIA sala do chat."""
    canal = message.channel
    if not isinstance(canal, discord.VoiceChannel):
        return  # so funciona no chat de uma sala de voz
    partes = linha.split(None, 1)
    if not partes:
        return
    cmd = partes[0].lower()
    arg = partes[1].strip() if len(partes) > 1 else ""
    quem = message.author.display_name or message.author.name
    guild = message.guild
    todos = {"tocar", "play", "p", "fila", "queue", "q", "agora", "pular", "skip", "pausar", "pause",
             "continuar", "resume", "parar", "stop", "sair", "leave", "remover", "embaralhar", "shuffle", "ajuda", "help",
             "voltar", "alternar", "painel", "volume", "vol", "telao"}
    if cmd not in todos:
        return  # nao e um comando nosso: ignora (nao apaga a mensagem)

    # Comandos de musica (tocar, pular, voltar, pausar/continuar) ficam visiveis no chat.
    # Todo o resto (telao, painel, volume...) e apagado na hora para o chat nao encher
    # (precisa de "Gerenciar mensagens" no canal; se faltar, avisa uma vez no console).
    visiveis = {"tocar", "play", "p", "pular", "skip", "voltar", "pausar", "pause", "continuar", "resume", "alternar"}
    if cmd not in visiveis:
        try:
            await message.delete()
        except discord.Forbidden:
            global _avisou_apagar
            if not _avisou_apagar:
                _avisou_apagar = True
                print("[aviso] Nao consegui apagar um comando do chat: de ao bot a permissao 'Gerenciar mensagens' nesse canal.")
        except Exception:
            pass

    async def dizer(texto: str):
        await canal.send(texto, allowed_mentions=discord.AllowedMentions.none())

    t = bot.tocador(guild)
    t.canal_texto = canal
    loop = asyncio.get_running_loop()

    if cmd in ("ajuda", "help"):
        await dizer(AJUDA_CHAT)
        return

    if cmd in ("volume", "vol"):
        v = t.volume
        if arg in ("+", "mais"):
            v += 5
        elif arg in ("-", "menos"):
            v -= 5
        else:
            try:
                v = int(arg.rstrip("%"))
            except ValueError:
                await dizer("Use assim: `!volume +`, `!volume -` ou `!volume 80`.")
                return
        t.volume = max(0, min(150, v))
        t.aplicar_volume()
        t.pedir_painel()
        return

    if cmd == "telao":
        sub = arg.split()
        # So existe UM telao por vez (ele mostra o que esta tocando no Discord; dois dariam conflito).
        partes = (t.telao or "").split(" | ")
        dono_atual = partes[7] if len(partes) >= 8 else ""
        eu = plano(quem, 30)
        if sub and sub[0].lower() in ("liberar", "forcar"):
            # saida de emergencia: o dono saiu do jogo e o telao ficou preso
            if t.telao:
                await dizer(f"\U0001F4FA O telao de **{dono_atual or '?'}** foi liberado por **{eu}**.")
            t.telao = None
            t.pedir_painel()
            return
        if sub and sub[0].lower() in ("recolher", "remover"):
            if t.telao and dono_atual and dono_atual != eu:
                await dizer(f"\U0001F6AB O telao foi criado por **{dono_atual}**; so essa pessoa pode recolher. "
                            f"Se ela saiu do jogo, use `!telao liberar`.")
                return
            t.telao = None
            t.pedir_painel()
            return
        if sub and sub[0].lower() == "video":
            # !telao video <link ou endereco do arquivo no Discord>
            alvo = sub[1] if len(sub) > 1 else ""
            if not link_de_video_ok(alvo):
                await dizer("Telao: o endereco do video e invalido (use um link http ou https).")
                return
            t.video = f"{time.time():.0f} | {alvo} | {plano(quem, 30)}"
            vc = t.vc
            if vc is not None and vc.is_playing():
                vc.pause()
                t.pausado_video = True
            # quem nao tem o mod ve (e ouve) o video pelo chat; arquivos enviados ja estao no chat
            if not re.match(r"^https://(cdn|media)\.discordapp\.(com|net)/", alvo):
                await dizer(f"\U0001F3AC **{plano(quem, 30)}** exibiu no telao: {alvo}")
            t.pedir_painel()
            return
        if sub and sub[0].lower() in ("videofim", "videoparar"):
            t.video = None
            vc = t.vc
            if t.pausado_video:
                t.pausado_video = False
                if vc is not None and vc.is_paused():
                    vc.resume()
            t.pedir_painel()
            return
        if sub and sub[0].lower() in ("criar", "ajustar") and len(sub) == 8:
            try:
                terr = int(sub[1])
                x, y, z, w, h, g = (float(n) for n in sub[2:8])
            except ValueError:
                await dizer("Telao: valores invalidos.")
                return
            if t.telao and dono_atual and dono_atual != eu:
                await dizer(f"\U0001F6AB Ja existe um telao em uso, criado por **{dono_atual}** "
                            f"(mapa n\u00ba {partes[0]}, x {partes[1]}, y {partes[2]}, z {partes[3]}). "
                            f"So pode haver um telao por vez; peca para **{dono_atual}** recolher, "
                            f"ou use `!telao liberar` se a pessoa saiu do jogo.")
                t.pedir_painel()
                return
            w = max(1.0, min(60.0, w))
            h = max(1.0, min(40.0, h))
            t.telao = f"{terr} | {x:.2f} | {y:.2f} | {z:.2f} | {w:.1f} | {h:.1f} | {g % 360:.0f} | {plano(quem, 30)}"
            t.pedir_painel()
        return

    if cmd == "painel":
        t.pedir_painel()  # reedita (ou cria) o painel para o plugin ler o estado atual
        return

    if cmd in ("tocar", "play", "p"):
        if not arg:
            await dizer("Escreva o nome da musica ou um link do YouTube. Ex.: `!tocar nome da musica`")
            return
        if "spotify.com" in arg.lower():
            await dizer("Links do Spotify ainda nao funcionam. Use o nome da musica ou um link do YouTube.")
            return
        try:
            itens = await loop.run_in_executor(None, _buscar, arg)
        except Exception as e:
            print(f"[erro] Busca falhou ('{arg}'): {e}")
            await dizer("Nao consegui achar essa musica. Tente outro nome ou link.")
            return
        if not itens:
            await dizer("Nao achei nada com essa busca.")
            return
        espaco = MAX_FILA - len(t.fila)
        if espaco <= 0:
            await dizer("A fila esta cheia.")
            return
        itens = itens[:espaco]
        vc = guild.voice_client
        try:
            if vc is None or not vc.is_connected():
                await canal.connect(self_deaf=True)
            elif vc.channel.id != canal.id:
                if t.ocupado() or t.fila:
                    await dizer("Ja estou tocando em outra sala de voz deste servidor.")
                    return
                await vc.move_to(canal)
        except Exception as e:
            print(f"[erro] Nao consegui entrar na sala: {e}")
            await dizer("Nao consegui entrar na sala de voz. Confira as permissoes 'Conectar' e 'Falar'.")
            return
        esperando = t.ocupado() or len(t.fila) > 0
        for it in itens:
            t.fila.append(Musica(it["url"], it["titulo"], it["duracao"], quem))
            print(f"[acao] Na fila: {it['titulo']} (pedido por {quem})")
        if len(itens) > 1:
            await dizer(f"{len(itens)} musicas adicionadas a fila (pedido de {curto(quem, 30)}).")
        elif esperando:
            await dizer(
                f"Adicionada a fila: **{curto(itens[0]['titulo'])}** [{fmt_duracao(itens[0]['duracao'])}] "
                f"- pedido de {curto(quem, 30)}"
            )
        await t.tocar_proxima()  # se estava livre, ele mesmo avisa "Tocando agora"
        t.verificar_sala()
        t.pedir_painel()
        return

    if cmd in ("fila", "queue", "q"):
        if t.atual is None and not t.fila:
            await dizer("A fila esta vazia.")
            return
        linhas = []
        if t.atual:
            linhas.append(
                f"**Tocando agora:** {curto(t.atual.titulo)} [{fmt_duracao(t.atual.duracao)}] "
                f"- pedido de {curto(t.atual.pedido_por, 30)}"
            )
        proximas = list(t.fila)
        for i, m in enumerate(proximas[:10], 1):
            linhas.append(f"{i}. {curto(m.titulo)} [{fmt_duracao(m.duracao)}] - {curto(m.pedido_por, 30)}")
        if len(proximas) > 10:
            linhas.append(f"... e mais {len(proximas) - 10} na fila.")
        await dizer("\n".join(linhas))
        return

    if cmd == "agora":
        if t.atual is None:
            await dizer("Nao tem nada tocando agora.")
        else:
            m = t.atual
            await dizer(
                f"Tocando agora: **{curto(m.titulo)}** [{fmt_duracao(m.duracao)}] - pedido de {curto(m.pedido_por, 30)}"
            )
        return

    # os comandos abaixo precisam do bot na sala
    vc = t.vc
    if vc is None or not vc.is_connected():
        await dizer("Nao estou tocando nada. Use `!tocar nome da musica`.")
        return

    if cmd in ("pular", "skip"):
        if t.atual is None or not (vc.is_playing() or vc.is_paused()):
            await dizer("Nao tem nada tocando para pular.")
            return
        titulo = t.atual.titulo
        t.ultimo_pulo = time.monotonic()
        vc.stop()
        print(f"[acao] Musica pulada por {quem}: {titulo}")
        await dizer(f"{curto(quem, 30)} pulou: **{curto(titulo)}**")
    elif cmd in ("pausar", "pause"):
        if vc.is_playing():
            vc.pause()
            t.pedir_painel()
            print(f"[acao] Musica pausada por {quem}")
            await dizer("Musica pausada. Use `!continuar` para voltar.")
        else:
            await dizer("Nao tem nada tocando para pausar.")
    elif cmd in ("continuar", "resume"):
        if vc.is_paused():
            vc.resume()
            t.pedir_painel()
            print(f"[acao] Musica continuou por {quem}")
            await dizer("Voltei a tocar.")
        else:
            await dizer("A musica nao esta pausada.")
    elif cmd == "alternar":
        if vc.is_playing():
            vc.pause()
            t.pedir_painel()
        elif vc.is_paused():
            vc.resume()
            t.pedir_painel()
        else:
            await dizer("Nao tem nada tocando.")
    elif cmd == "voltar":
        if t.anterior is None:
            await dizer("Ainda nao tem musica anterior.")
            return
        a = t.anterior
        if len(t.fila) >= MAX_FILA:
            await dizer("A fila esta cheia.")
            return
        t.fila.append(Musica(a.url, a.titulo, a.duracao, quem))
        await dizer(f"Voltei a colocar na fila: **{curto(a.titulo)}**")
        await t.tocar_proxima()
        t.pedir_painel()
    elif cmd in ("parar", "stop"):
        await t.parar()
        await dizer("Parei a musica e limpei a fila.")
    elif cmd in ("sair", "leave"):
        await dizer("Saindo da sala. Ate mais!")
        await t.encerrar()
    elif cmd == "remover":
        try:
            n = int(arg)
        except ValueError:
            await dizer("Use assim: `!remover 2` (o numero aparece em `!fila`).")
            return
        if n < 1 or n > len(t.fila):
            await dizer("Esse numero nao existe na fila.")
            return
        lista = list(t.fila)
        removida = lista.pop(n - 1)
        t.fila = deque(lista)
        t.pedir_painel()
        await dizer(f"Removi da fila: **{curto(removida.titulo)}**")
    elif cmd in ("embaralhar", "shuffle"):
        if len(t.fila) < 2:
            await dizer("Precisa ter pelo menos 2 musicas na fila.")
            return
        lista = list(t.fila)
        random.shuffle(lista)
        t.fila = deque(lista)
        t.pedir_painel()
        await dizer("Fila embaralhada.")


def _versao(nome: str) -> str:
    try:
        from importlib.metadata import version
        return version(nome)
    except Exception:
        return "NAO ENCONTRADO"


def diagnostico(vc=None, canal=None, guild=None) -> list:
    linhas = [
        f"discord.py: {discord.__version__}",
        f"davey (criptografia de voz): {_versao('davey')}",
        f"PyNaCl: {_versao('PyNaCl')}",
        f"yt-dlp: {_versao('yt-dlp')}",
        f"opus carregado: {discord.opus.is_loaded()}",
        f"ffmpeg: {shutil.which('ffmpeg') or 'NAO ENCONTRADO'}",
        f"deno: {shutil.which('deno') or 'NAO ENCONTRADO'}",
    ]
    try:
        if vc is not None:
            try:
                lat = f"{vc.latency * 1000:.0f} ms"
            except Exception:
                lat = "desconhecida"
            linhas.append(f"voz conectada: {vc.is_connected()} | latencia de voz: {lat}")
            linhas.append(f"protocolo DAVE: {getattr(vc, 'dave_protocol_version', 'sem informacao')}")
            linhas.append(f"codigo de privacidade: {getattr(vc, 'voice_privacy_code', 'sem informacao')}")
        if canal is not None and guild is not None and guild.me is not None:
            p = canal.permissions_for(guild.me)
            linhas.append(
                f"permissoes do bot na sala '{canal.name}': Conectar={p.connect} | Falar={p.speak} "
                f"| Voz por deteccao={p.use_voice_activation}"
            )
        est = guild.me.voice if guild is not None and guild.me is not None else None
        if est is not None:
            linhas.append(
                f"mudo pelo servidor={est.mute} | surdo pelo servidor={est.deaf} | "
                f"proprio mudo={est.self_mute} | proprio surdo={est.self_deaf} | suprimido={est.suppress}"
            )
    except Exception as e:
        linhas.append(f"(erro ao ler os detalhes: {e!r})")
    return linhas


@bot.tree.command(name="teste", description="Toca um apito de 4 segundos (testa se o som do bot chega na sala)")
@app_commands.guild_only()
async def teste(interaction: discord.Interaction):
    canal = _canal_do_usuario(interaction)
    if canal is None:
        await interaction.response.send_message("Entre em uma sala de voz primeiro.", ephemeral=True)
        return
    await interaction.response.defer(ephemeral=True)
    t = bot.tocador(interaction.guild)
    vc = interaction.guild.voice_client
    if vc is not None and vc.is_connected() and (t.ocupado() or t.fila):
        await interaction.followup.send("O bot esta tocando musica. Use /parar e tente de novo.", ephemeral=True)
        return
    try:
        if vc is None or not vc.is_connected():
            vc = await canal.connect(self_deaf=True)
        elif vc.channel.id != canal.id:
            await vc.move_to(canal)
    except Exception as e:
        print(f"[erro] /teste: nao consegui entrar na sala: {e}")
        await interaction.followup.send("Nao consegui entrar na sala de voz.", ephemeral=True)
        return
    t.canal_texto = interaction.channel
    try:
        fonte = discord.FFmpegPCMAudio(
            "sine=frequency=440:duration=4",
            executable=shutil.which("ffmpeg") or "ffmpeg",
            before_options="-f lavfi",
            options="-vn -af volume=0.3",
        )
        vc.play(fonte, after=lambda e: print(f"[teste] o apito terminou. erro: {e}"))
    except Exception as e:
        print(f"[erro] /teste: nao consegui tocar o apito: {e!r}")
        await interaction.followup.send(f"Nao consegui tocar o apito: {e}", ephemeral=True)
        return
    await asyncio.sleep(3.5)  # da tempo do Discord atualizar o estado de voz do bot
    linhas = diagnostico(vc, canal, interaction.guild)
    print()
    print("[teste] Apito de 4 segundos enviado. Informacoes do bot:")
    for linha in linhas:
        print("   " + linha)
    print()
    await interaction.followup.send(
        "Tocando um apito por 4 segundos. **Voce ouviu?**\n" + "\n".join(linhas), ephemeral=True
    )


@bot.tree.error
async def erro_comando(interaction: discord.Interaction, error: app_commands.AppCommandError):
    print(f"[erro] Comando falhou: {error!r}")
    msg = "Algo deu errado. Tente de novo."
    try:
        if interaction.response.is_done():
            await interaction.followup.send(msg, ephemeral=True)
        else:
            await interaction.response.send_message(msg, ephemeral=True)
    except Exception:
        pass


# ---------------------------------------------------------------- inicio

def escolher_configuracao():
    """Ao abrir, pergunta se mantem as configuracoes recentes ou comeca de novo (outra sala / outro bot)."""
    if SEM_PERGUNTAS:
        return
    cfg = ler_config()
    sala = str(cfg.get("canal_chat_id", "")).strip()
    tem_token = os.path.exists(ARQ_TOKEN)
    if not sala.isdigit() and not tem_token:
        return  # primeira vez: nada para perguntar
    print()
    print("=== Configuracoes salvas ===")
    print(f"  Sala de texto: {sala if sala.isdigit() else '(nenhuma)'}")
    print(f"  Token do bot:  {'salvo' if tem_token else '(nenhum)'}")
    print()
    print("  [Enter] ou 1 = Manter as configuracoes recentes")
    print("  2 = Trocar so a sala (vou pedir o ID da nova sala)")
    print("  3 = Comecar do zero (nova sala e novo token)")
    print()
    while True:
        r = input("Escolha (Enter = manter): ").strip()
        if r in ("", "1"):
            return
        if r == "2":
            cfg["canal_chat_id"] = ""
            salvar_config(cfg)
            print("Ok: vou pedir a nova sala.")
            return
        if r == "3":
            cfg["canal_chat_id"] = ""
            salvar_config(cfg)
            try:
                os.remove(ARQ_TOKEN)
            except OSError:
                pass
            print("Ok: vou pedir a nova sala e o token.")
            return
        print("Digite 1, 2 ou 3 (ou so Enter).")


def garantir_sala_de_chat():
    """Na primeira vez (config sem sala), pergunta o ID da sala de texto onde o FFBL conversa com o bot."""
    cfg = ler_config()
    if str(cfg.get("canal_chat_id", "")).strip().isdigit():
        return
    if SEM_PERGUNTAS:
        print("ERRO: falta o ID da sala. Escolha uma sala no painel e ligue de novo.")
        sys.exit(1)
    print()
    print("Falta dizer qual e a SALA DE TEXTO do FFBL (a sala onde esta o webhook).")
    print("No Discord: botao direito na sala > Copiar ID do canal.")
    print("(Se nao aparece essa opcao: Configuracoes > Avancado > Modo desenvolvedor.)")
    while True:
        novo = input("ID da sala de texto (so numeros): ").strip()
        if novo.isdigit():
            break
        print("Precisa ser so numeros. Tente de novo.")
    cfg["canal_chat_id"] = novo
    salvar_config(cfg)
    print("Salvo! Na proxima vez que abrir, o bot pergunta se voce quer manter ou trocar.")
    print()


def configurar_logs():
    """Registro de erros e da conexao de voz (usado pelo modo console e pela janela)."""
    try:
        open(ARQ_LOG, "w").close()  # comeca um registro de erros novo a cada abertura
    except OSError:
        pass
    # mostra na janela o que acontece com a conexao de voz (ajuda a achar o problema do som)
    for nome in ("discord.voice_client", "discord.player"):
        logging.getLogger(nome).setLevel(logging.INFO)
    logging.getLogger("discord.voice_state").setLevel(logging.DEBUG)  # inclui a negociacao da criptografia
    # grava tudo da voz num arquivo (voz.log) para eu poder ler depois
    try:
        fh = logging.FileHandler("voz.log", mode="w", encoding="utf-8")
        fh.setFormatter(logging.Formatter("%(asctime)s %(name)s %(levelname)s %(message)s"))
        for nome in ("discord.voice_state", "discord.voice_client", "discord.player", "discord.gateway"):
            lg = logging.getLogger(nome)
            lg.addHandler(fh)
            if nome == "discord.gateway":
                lg.setLevel(logging.DEBUG)
    except OSError:
        pass


def main():
    if not shutil.which("ffmpeg"):
        print("ERRO: o programa de audio (ffmpeg) nao foi encontrado.")
        print("Rode o INSTALAR.bat de novo.")
        input("Aperte Enter para sair.")
        sys.exit(1)
    escolher_configuracao()
    token = ler_token()
    garantir_sala_de_chat()
    configurar_logs()
    try:
        bot.run(token, log_level=logging.WARNING)
    except discord.PrivilegedIntentsRequired:
        print()
        print("ERRO: falta ligar uma opcao no portal do Discord.")
        print("1. Abra o portal do Discord e entre no app JBLEZA.")
        print("2. Menu 'Bot' > role ate 'Privileged Gateway Intents'.")
        print("3. Ligue 'Message Content Intent' e clique em Salvar.")
        print("4. Abra o INICIAR.bat de novo.")
        input("Aperte Enter para sair.")
        sys.exit(1)
    except discord.LoginFailure:
        try:
            os.remove(ARQ_TOKEN)
        except OSError:
            pass
        print()
        print("ERRO: o token e invalido. Apaguei o token salvo.")
        print("Copie o token de novo no portal do Discord e abra o INICIAR.bat outra vez.")
        input("Aperte Enter para sair.")
        sys.exit(1)


if __name__ == "__main__":
    main()
