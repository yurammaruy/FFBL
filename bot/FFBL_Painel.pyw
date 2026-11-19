# -*- coding: utf-8 -*-
"""
FFBL - Painel do bot de musica
- guarda as salas ja usadas (so UMA fica ligada por vez)
- ao minimizar, vai para a bandeja do relogio (se o pacote pystray estiver instalado)
- mostra o que o bot esta fazendo
"""
import base64
import io
import json
import os
import queue
import re
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
import zlib
import tkinter as tk
from tkinter import messagebox, scrolledtext, simpledialog, ttk

try:
    import pystray
    from PIL import Image
except Exception:  # sem bandeja: minimizar fica normal na barra de tarefas
    pystray = None
    Image = None

# Cores inspiradas em Final Fantasy: azul-noite, dourado, cristal e creme
BG = "#0B1630"
CARD = "#12234A"
CAMPO = "#1A2E5C"
CAMPO_TRAVADO = "#0F1C3B"
BOTAO = "#1F3A75"
BOTAO_ATIVO = "#2D52A3"
OURO = "#E3C26B"
CRISTAL = "#7FD4FF"
TEXTO = "#F1EBD6"
APAGADO = "#9FB3D9"
ALERTA = "#FF9E73"
ERRO = "#FF6B6B"

ICONE_B64 = "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"

try:  # no Windows, o icone da barra de tarefas passa a ser o do FFBL (e nao o do Python)
    import ctypes
    ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("FFBL.PainelDoBot")
except Exception:
    pass

PASTA = os.path.dirname(os.path.abspath(__file__))
ARQ_SALAS = os.path.join(PASTA, "salas.json")
ARQ_CONFIG = os.path.join(PASTA, "config.json")
ARQ_TOKEN = os.path.join(PASTA, "token.txt")
CAMPOS = [("canal", "Nome do Canal:", False), ("nome", "Nome da Sala:", False), ("client_id", "Client ID:", False),
          ("client_secret", "Client Secret:", True), ("id", "ID da SALA:", False), ("token", "Token do bot:", True),
          ("webhook", "Link do Webhook:", True)]
PY = os.path.join(PASTA, "venv", "Scripts", "python.exe")
ICONE = next((p for p in (os.path.join(PASTA, "icon.png"), os.path.join(PASTA, "..", "images", "icon.png"))
              if os.path.exists(p)), "")
SEM_JANELA = 0x08000000  # CREATE_NO_WINDOW (so no Windows)
LIMITE_LOG = 1500


def ler_json(caminho, padrao):
    try:
        with open(caminho, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return padrao


def gravar_json(caminho, dados):
    with open(caminho, "w", encoding="utf-8") as f:
        json.dump(dados, f, ensure_ascii=False, indent=2)


def gerar_codigo(client_id, secret, webhook_url, sala_id):
    """Codigo de conexao: FFBL1- + base64url('id|segredo|webhookId/token|sala' + CRC32)."""
    m = re.match(r"^https://(?:discord|discordapp)\.com/api/webhooks/(\d+)/([\w-]+)$", webhook_url.strip())
    if not m:
        raise ValueError("O link do webhook nao parece valido.")
    for nome, v in (("Client ID", client_id), ("Client Secret", secret), ("ID da sala", sala_id)):
        if not v or "|" in v:
            raise ValueError(f"{nome} esta vazio ou invalido.")
    corpo = "|".join([client_id, secret, f"{m.group(1)}/{m.group(2)}", sala_id]).encode("utf-8")
    crc = zlib.crc32(corpo).to_bytes(4, "big")
    return "FFBL1-" + base64.urlsafe_b64encode(corpo + crc).decode("ascii").rstrip("=")


def webhook_automatico(token_bot, sala_id):
    """Tenta achar (ou criar) um webhook chamado FFBL na sala. Devolve a URL ou levanta erro."""
    base = f"https://discord.com/api/v10/channels/{sala_id}/webhooks"
    cab = {"Authorization": f"Bot {token_bot}", "Content-Type": "application/json",
           "User-Agent": "DiscordBot (https://github.com/yurammaruy/FFBL, 1.0)"}

    def chamar(metodo, corpo=None):
        req = urllib.request.Request(base, data=json.dumps(corpo).encode() if corpo else None,
                                     headers=cab, method=metodo)
        with urllib.request.urlopen(req, timeout=15) as r:
            return json.loads(r.read().decode("utf-8"))

    for w in chamar("GET"):
        if w.get("name") == "FFBL" and w.get("token"):
            return f"https://discord.com/api/webhooks/{w['id']}/{w['token']}"
    w = chamar("POST", {"name": "FFBL"})
    return f"https://discord.com/api/webhooks/{w['id']}/{w['token']}"


def traduzir(linha):
    """Transforma uma linha tecnica do bot em uma frase simples. Devolve None se nao for importante."""
    t = linha.strip()
    if not t:
        return None
    if t.startswith("[acao]"):
        return t[6:].strip()
    if t.startswith("[erro]"):
        return "Problema: " + t[6:].strip()[:200]
    if t.startswith("[aviso]"):
        return "Aviso: " + t[7:].strip()[:200]
    if t.startswith("ERRO:"):
        return "Problema: " + t[5:].strip()[:200]
    if t.startswith("[ok] Comandos prontos no servidor:"):
        return "Comandos prontos no servidor " + t.split(":", 1)[1].strip()
    if t.startswith("[chat] Sala do chat:"):
        return "Sala de texto conectada: " + t.split(":", 1)[1].strip()
    if t.startswith("[chat] Atencao:"):
        return "Atencao com a sala de texto: " + t.split(":", 1)[1].strip()
    if t.startswith("[teste]"):
        return "Teste de som: " + t[7:].strip()
    return None


class AbaSala(ttk.Frame):
    """Uma aba = uma sala. Depois de salva, os campos ficam travados ate clicar em Editar."""

    def __init__(self, painel, sala):
        super().__init__(painel.abas, padding=12)
        self.painel = painel
        self.sala = sala
        self.vars = {}
        self.entradas = {}
        self.columnconfigure(1, weight=1)
        for i, (chave, rotulo, oculto) in enumerate(CAMPOS):
            ttk.Label(self, text=rotulo).grid(row=i, column=0, sticky="w", pady=3)
            v = tk.StringVar(value=sala.get(chave, ""))
            e = ttk.Entry(self, textvariable=v, show="*" if oculto else "")
            e.grid(row=i, column=1, sticky="ew", padx=6)
            self.vars[chave] = v
            self.entradas[chave] = e
        n = len(CAMPOS)
        barra = ttk.Frame(self)
        barra.grid(row=n, column=0, columnspan=2, sticky="w", pady=8)
        self.b_salvar = ttk.Button(barra, text="Salvar", command=self.salvar)
        self.b_apagar = ttk.Button(barra, text="Apagar", command=lambda: painel.apagar(self))
        self.b_iniciar = ttk.Button(barra, text="Iniciar bot", command=lambda: painel.iniciar(self))
        self.b_editar = ttk.Button(barra, text="Editar informacoes", command=self.editar)
        for b in (self.b_salvar, self.b_apagar, self.b_iniciar, self.b_editar):
            b.pack(side="left", padx=(0, 6))
        ttk.Label(self, text="Codigo de conexao (e o que o jogador cola no FFBL):").grid(
            row=n + 1, column=0, columnspan=2, sticky="w")
        self.v_cod = tk.StringVar()
        ttk.Entry(self, textvariable=self.v_cod, state="readonly").grid(
            row=n + 2, column=0, columnspan=2, sticky="ew", pady=3)
        ttk.Button(self, text="Copiar", command=self.copiar).grid(row=n + 3, column=0, sticky="w", pady=3)
        ttk.Label(
            self, wraplength=680, foreground=ALERTA,
            text="Cuidado: quem tem o codigo escreve na sala e usa o Client Secret do seu app. Mande so para quem voce "
                 "confia. O token do bot NAO vai dentro do codigo e nunca deve ser compartilhado."
        ).grid(row=n + 4, column=0, columnspan=2, sticky="w", pady=(6, 0))
        self.travar(bool(sala.get("salva")))

    def valores(self):
        return {k: v.get().strip().strip('"') for k, v in self.vars.items()}

    def travar(self, travado):
        for e in self.entradas.values():
            e.config(state="readonly" if travado else "normal")
        self.b_salvar.config(state="disabled" if travado else "normal")
        self.b_editar.config(state="normal" if travado else "disabled")
        self.b_iniciar.config(state="normal" if travado else "disabled")
        self.v_cod.set(self.codigo() if travado else "")

    def codigo(self):
        d = self.sala
        try:
            return gerar_codigo(d.get("client_id", ""), d.get("client_secret", ""), d.get("webhook", ""), d.get("id", ""))
        except ValueError:
            return ""

    def salvar(self):
        v = self.valores()
        falta = [rot for (k, rot, _) in CAMPOS if not v[k]]
        if falta:
            messagebox.showwarning("Faltam informacoes", "Preencha: " + ", ".join(f.rstrip(":") for f in falta), parent=self)
            return
        if not v["id"].isdigit():
            messagebox.showwarning("ID invalido", "O ID da SALA precisa ser so numeros.", parent=self)
            return
        try:
            gerar_codigo(v["client_id"], v["client_secret"], v["webhook"], v["id"])
        except ValueError as e:
            messagebox.showwarning("Verifique", str(e), parent=self)
            return
        self.sala.update(v)
        self.sala["salva"] = True
        self.painel.salvar_tudo()
        self.painel.atualizar_titulos()
        self.travar(True)

    def editar(self):
        if self.painel.sala_ativa is self.sala:
            messagebox.showinfo("Bot ligado", "Desligue o bot dessa sala antes de editar.", parent=self)
            return
        self.sala["salva"] = False
        self.travar(False)

    def copiar(self):
        if self.v_cod.get():
            self.clipboard_clear()
            self.clipboard_append(self.v_cod.get())
            messagebox.showinfo("Copiado", "Codigo copiado. Mande para a pessoa.", parent=self)


class Painel(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("FFBL - Bot de musica")
        self.geometry("780x700")
        self.minsize(720, 620)
        self.configure(bg=BG)
        self._estilo()
        self.ultimo_basico = ""
        self.proc = None
        self.sala_ativa = None
        self.fila = queue.Queue()
        self.bandeja = None
        self.dados = ler_json(ARQ_SALAS, {"salas": []})
        for s in self.dados["salas"]:  # salas da versao antiga (so nome e ID) ficam abertas para completar
            s.setdefault("salva", False)
        try:
            self.iconphoto(True, tk.PhotoImage(data=ICONE_B64))
        except Exception:
            pass
        topo = ttk.Frame(self, padding=(10, 10, 10, 0))
        topo.pack(fill="x")
        ttk.Button(topo, text="+ Nova sala", command=self.nova).pack(side="left")
        self.lbl_status = ttk.Label(topo, text="Desligado", font=("Segoe UI", 10, "bold"), foreground=CRISTAL)
        self.lbl_status.pack(side="left", padx=14)
        ttk.Button(topo, text="Desligar bot", command=self.desligar).pack(side="right")
        self.abas = ttk.Notebook(self)
        self.abas.pack(fill="x", padx=10, pady=8)
        self.paginas = []
        for s in self.dados["salas"]:
            self._criar_aba(s)
        ttk.Label(self, text="O que o bot esta fazendo:", padding=(10, 0), foreground=OURO).pack(anchor="w")
        self.abas_log = ttk.Notebook(self)
        self.abas_log.pack(fill="both", expand=True, padx=10, pady=(2, 10))
        self.log_basico = self._criar_log(self.abas_log, "Basico", "Consolas", 10)
        self.log_detalhado = self._criar_log(self.abas_log, "Detalhado", "Consolas", 9)
        self.log = self.log_detalhado
        self.protocol("WM_DELETE_WINDOW", self._fechar)
        self.bind("<Unmap>", self._ao_minimizar)
        self.after(200, self._ler_fila)

    # ------------------------------------------------------------ aparencia
    def _estilo(self):
        st = ttk.Style(self)
        try:
            st.theme_use("clam")
        except Exception:
            pass
        st.configure(".", background=BG, foreground=TEXTO, fieldbackground=CAMPO, bordercolor=OURO,
                     lightcolor=BG, darkcolor=BG, troughcolor=CARD, focuscolor=OURO)
        st.configure("TFrame", background=BG)
        st.configure("TLabel", background=BG, foreground=TEXTO)
        st.configure("TButton", background=BOTAO, foreground=OURO, bordercolor=OURO, padding=(10, 4), relief="flat")
        st.map("TButton", background=[("active", BOTAO_ATIVO), ("disabled", CARD)],
               foreground=[("disabled", APAGADO)])
        st.configure("TEntry", fieldbackground=CAMPO, foreground=TEXTO, insertcolor=OURO, bordercolor=BOTAO)
        st.map("TEntry", fieldbackground=[("readonly", CAMPO_TRAVADO)], foreground=[("readonly", CRISTAL)])
        st.configure("TNotebook", background=BG, bordercolor=OURO, tabmargins=(2, 4, 2, 0))
        st.configure("TNotebook.Tab", background=CARD, foreground=APAGADO, padding=(14, 5), bordercolor=BOTAO)
        st.map("TNotebook.Tab", background=[("selected", BOTAO)], foreground=[("selected", OURO)])

    def _criar_log(self, notebook, titulo, fonte, tamanho):
        quadro = ttk.Frame(notebook)
        caixa = scrolledtext.ScrolledText(quadro, height=10, state="disabled", font=(fonte, tamanho), bg="#07101F",
                                          fg="#CFE6FF", insertbackground=OURO, relief="flat", wrap="word")
        caixa.pack(fill="both", expand=True)
        caixa.tag_configure("ouro", foreground=OURO)
        caixa.tag_configure("alerta", foreground=ALERTA)
        caixa.tag_configure("erro", foreground=ERRO)
        caixa.tag_configure("cristal", foreground=CRISTAL)
        notebook.add(quadro, text=titulo)
        return caixa

    # ------------------------------------------------------------ abas
    def _criar_aba(self, sala):
        aba = AbaSala(self, sala)
        self.paginas.append(aba)
        self.abas.add(aba, text=self._titulo(sala))
        return aba

    def _titulo(self, sala):
        t = sala.get("nome") or "Nova sala"
        return t + ("  *" if self.sala_ativa is sala else "")

    def atualizar_titulos(self):
        for aba in self.paginas:
            self.abas.tab(aba, text=self._titulo(aba.sala))

    def salvar_tudo(self):
        gravar_json(ARQ_SALAS, self.dados)

    def nova(self):
        sala = {"salva": False}
        self.dados["salas"].append(sala)
        aba = self._criar_aba(sala)
        self.abas.select(aba)

    def apagar(self, aba):
        if self.sala_ativa is aba.sala:
            if not messagebox.askyesno("Sala ligada", "O bot esta ligado nessa sala. Desligar e apagar?", parent=self):
                return
            self.desligar()
        if not messagebox.askyesno("Apagar", f'Apagar a sala "{aba.sala.get("nome") or "nova"}"?', parent=self):
            return
        self.dados["salas"].remove(aba.sala)
        self.paginas.remove(aba)
        self.abas.forget(aba)
        aba.destroy()
        self.salvar_tudo()

    # ------------------------------------------------------------ ligar / desligar
    def iniciar(self, aba):
        sala = aba.sala
        if not sala.get("token") or not sala.get("id"):
            messagebox.showinfo("Sala", "Falta o token ou o ID da sala. Clique em Editar informacoes.", parent=self)
            return
        if not os.path.exists(PY):
            messagebox.showerror("Instalacao", "O bot ainda nao foi instalado. Rode o INSTALAR.bat primeiro.", parent=self)
            return
        if self.proc is not None:
            if self.sala_ativa is sala:
                return
            if not messagebox.askyesno(
                    "Trocar de sala",
                    f'O bot esta ligado na sala "{self.sala_ativa.get("nome")}". Desligar e ligar nesta?', parent=self):
                return
            self.desligar()

        with open(ARQ_TOKEN, "w", encoding="utf-8") as f:
            f.write(sala["token"])
        cfg = ler_json(ARQ_CONFIG, {})
        cfg["canal_chat_id"] = sala["id"]
        gravar_json(ARQ_CONFIG, cfg)

        env = os.environ.copy()
        env["PATH"] = os.path.join(PASTA, "ferramentas") + os.pathsep + env.get("PATH", "")
        env["PYTHONUTF8"] = "1"
        env["FFBL_SEM_PERGUNTAS"] = "1"
        try:
            self.proc = subprocess.Popen(
                [PY, "-u", "bot.py"], cwd=PASTA, env=env, stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8",
                errors="replace", bufsize=1, creationflags=SEM_JANELA)
        except Exception as e:
            self.proc = None
            messagebox.showerror("Erro", f"Nao consegui ligar o bot: {e}", parent=self)
            return
        self.sala_ativa = sala
        self._escrever(f'--- ligando na sala: {sala.get("nome")} ({sala["id"]}) ---\n')
        threading.Thread(target=self._ler_saida, args=(self.proc,), daemon=True).start()
        self._estado()

    def _ler_saida(self, proc):
        try:
            for linha in proc.stdout:
                self.fila.put(linha)
        except Exception:
            pass
        self.fila.put(("fim", proc))

    def desligar(self):
        p = self.proc
        if p is None:
            return
        try:
            subprocess.run(["taskkill", "/PID", str(p.pid), "/T", "/F"], creationflags=SEM_JANELA,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
        except Exception:
            try:
                p.kill()
            except Exception:
                pass
        self.proc = None
        self.sala_ativa = None
        self._escrever("--- bot desligado ---\n")
        self._estado()

    def _estado(self):
        ligado = self.proc is not None
        self.lbl_status.config(text=f'Ligado na sala "{self.sala_ativa.get("nome")}"' if ligado else "Desligado")
        self.atualizar_titulos()

    # ------------------------------------------------------------ log
    def _inserir(self, caixa, texto, tag=None):
        caixa.config(state="normal")
        caixa.insert("end", texto, tag) if tag else caixa.insert("end", texto)
        linhas = int(caixa.index("end-1c").split(".")[0])
        if linhas > LIMITE_LOG:
            caixa.delete("1.0", f"{linhas - LIMITE_LOG}.0")
        caixa.see("end")
        caixa.config(state="disabled")

    def _basico(self, frase):
        """Mostra uma frase simples (sem repetir a mesma de antes)."""
        if not frase or frase == self.ultimo_basico:
            return
        self.ultimo_basico = frase
        tag = "erro" if frase.startswith("Problema") else "alerta" if frase.startswith(("Aviso", "Atencao")) else \
            "ouro" if frase.startswith("Tocando") else None
        self._inserir(self.log_basico, time.strftime("%H:%M:%S") + "  " + frase + "\n", tag)

    def _escrever(self, texto):
        """Texto do proprio painel: aparece nas duas abas."""
        self._inserir(self.log_detalhado, texto)
        t = texto.strip()
        if t.startswith("--- ligando na sala:"):
            self._basico("Ligando o bot na sala " + t.split(":", 1)[1].strip(" -") + "...")
        elif t == "--- bot desligado ---":
            self._basico("Bot desligado.")
        elif t == "--- o bot parou ---":
            self._basico("Problema: o bot parou sozinho. Veja a aba Detalhado para saber o motivo.")

    def _linha_do_bot(self, linha):
        self._inserir(self.log_detalhado, linha)
        self._basico(traduzir(linha))

    def _ler_fila(self):
        try:
            while True:
                item = self.fila.get_nowait()
                if isinstance(item, tuple) and item[0] == "fim":
                    if item[1] is self.proc:  # o bot fechou sozinho
                        self.proc = None
                        self.sala_ativa = None
                        self._escrever("--- o bot parou ---\n")
                        self._estado()
                else:
                    self._linha_do_bot(item)
        except queue.Empty:
            pass
        self.after(200, self._ler_fila)

    # ------------------------------------------------------------ bandeja do relogio
    def _ao_minimizar(self, _evento):
        if pystray is None or self.bandeja is not None:
            return
        if self.state() == "iconic":
            self.after(50, self._ir_para_bandeja)

    def _ir_para_bandeja(self):
        if self.bandeja is not None or self.state() != "iconic":
            return
        try:
            imagem = Image.open(io.BytesIO(base64.b64decode(ICONE_B64))).convert("RGBA")
            menu = pystray.Menu(
                pystray.MenuItem("Abrir o painel", lambda *_: self.after(0, self._abrir), default=True),
                pystray.MenuItem("Desligar o bot", lambda *_: self.after(0, self.desligar)),
                pystray.MenuItem("Sair", lambda *_: self.after(0, self._sair)),
            )
            self.bandeja = pystray.Icon("FFBL", imagem, "FFBL - Bot de musica", menu)
            self.bandeja.run_detached()
            self.withdraw()
        except Exception:
            self.bandeja = None  # se a bandeja falhar, fica na barra de tarefas normal

    def _abrir(self):
        if self.bandeja is not None:
            try:
                self.bandeja.stop()
            except Exception:
                pass
            self.bandeja = None
        self.deiconify()
        self.state("normal")
        self.lift()
        self.focus_force()

    # ------------------------------------------------------------ fechar
    def _fechar(self):
        if self.proc is not None and not messagebox.askyesno(
                "Fechar", "O bot esta ligado. Fechar o painel vai desligar a musica. Fechar mesmo?", parent=self):
            return
        self._sair()

    def _sair(self):
        self.desligar()
        if self.bandeja is not None:
            try:
                self.bandeja.stop()
            except Exception:
                pass
        self.destroy()


if __name__ == "__main__":
    Painel().mainloop()
