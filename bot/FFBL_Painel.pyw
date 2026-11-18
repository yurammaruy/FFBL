# -*- coding: utf-8 -*-
"""
FFBL - Painel do bot de musica
- guarda as salas ja usadas (so UMA fica ligada por vez)
- ao minimizar, vai para a bandeja do relogio (se o pacote pystray estiver instalado)
- mostra o que o bot esta fazendo
"""
import base64
import json
import os
import queue
import re
import subprocess
import sys
import threading
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
            self, wraplength=640, foreground="#a33",
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
        self.geometry("760x640")
        self.minsize(700, 560)
        self.proc = None
        self.sala_ativa = None
        self.fila = queue.Queue()
        self.bandeja = None
        self.dados = ler_json(ARQ_SALAS, {"salas": []})
        for s in self.dados["salas"]:  # salas da versao antiga (so nome e ID) ficam abertas para completar
            s.setdefault("salva", False)
        if ICONE:
            try:
                self.iconphoto(True, tk.PhotoImage(file=ICONE))
            except Exception:
                pass
        topo = ttk.Frame(self, padding=(10, 10, 10, 0))
        topo.pack(fill="x")
        ttk.Button(topo, text="+ Nova sala", command=self.nova).pack(side="left")
        self.lbl_status = ttk.Label(topo, text="Desligado", font=("Segoe UI", 10, "bold"))
        self.lbl_status.pack(side="left", padx=14)
        ttk.Button(topo, text="Desligar bot", command=self.desligar).pack(side="right")
        self.abas = ttk.Notebook(self)
        self.abas.pack(fill="x", padx=10, pady=8)
        self.paginas = []
        for s in self.dados["salas"]:
            self._criar_aba(s)
        ttk.Label(self, text="O que o bot esta fazendo:", padding=(10, 0)).pack(anchor="w")
        self.log = scrolledtext.ScrolledText(self, height=10, state="disabled", font=("Consolas", 9))
        self.log.pack(fill="both", expand=True, padx=10, pady=(2, 10))
        self.protocol("WM_DELETE_WINDOW", self._fechar)
        self.bind("<Unmap>", self._ao_minimizar)
        self.after(200, self._ler_fila)

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
    def _escrever(self, texto):
        self.log.config(state="normal")
        self.log.insert("end", texto)
        linhas = int(self.log.index("end-1c").split(".")[0])
        if linhas > LIMITE_LOG:
            self.log.delete("1.0", f"{linhas - LIMITE_LOG}.0")
        self.log.see("end")
        self.log.config(state="disabled")

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
                    self._escrever(item)
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
            imagem = Image.open(ICONE).resize((64, 64)) if ICONE else Image.new("RGB", (64, 64), (88, 101, 242))
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
