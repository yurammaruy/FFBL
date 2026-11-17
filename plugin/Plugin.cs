using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FFBL;

public sealed class Plugin : IDalamudPlugin
{
    private const string Comando = "/ffbl";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static ITextureReadbackProvider Leitura { get; private set; } = null!;

    internal Configuration Config { get; }
    internal ChatState Chat { get; } = new();
    internal ThumbCache Capas { get; } = new();
    internal FileDialogManager Dialogos { get; } = new();

    // Previa local do telao enquanto a pessoa mexe nos controles (antes do bot confirmar)
    internal TelaoInfo? Previa { get; private set; }
    private DateTime previaAte = DateTime.MinValue;

    internal void DefinirPrevia(TelaoInfo t, double segundos)
    {
        Previa = t;
        previaAte = DateTime.Now.AddSeconds(segundos);
    }

    private DateTime suprimirAte = DateTime.MinValue;

    internal TelaoInfo? TelaoEfetivo(TelaoInfo? doPainel)
    {
        if (DateTime.Now < suprimirAte) return null; // acabou de recolher: ignora o painel ate o bot confirmar
        return Previa != null && DateTime.Now < previaAte ? Previa : doPainel;
    }

    private readonly WindowSystem windowSystem = new("FFBL");
    private readonly MainWindow mainWindow;
    private readonly IconWindow iconWindow;
    internal TelaoMinion Minion { get; }
    internal OmneModulo Omne { get; }
    private readonly DiscordRpc rpc;

    private CancellationTokenSource? cts;
    private Task? tarefa;
    private DateTime ultimoEnvio = DateTime.MinValue;
    private readonly System.Threading.Channels.Channel<(string Url, string Nome, string Avatar, string Texto)> envios
        = System.Threading.Channels.Channel.CreateUnbounded<(string Url, string Nome, string Avatar, string Texto)>();
    private readonly CancellationTokenSource ctsEnvios = new();

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Loc.Idioma = string.IsNullOrEmpty(Config.Idioma) ? "pt" : Config.Idioma;
        rpc = new DiscordRpc(Config, SalvarConfig, Chat);
        rpc.Conectou += () => EnviarComo("!painel"); // pede o estado atual do painel assim que conecta

        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);
        iconWindow = new IconWindow(this);
        windowSystem.AddWindow(iconWindow);
        Minion = new TelaoMinion(this);
        Omne = new OmneModulo(this, Framework, TargetManager);

        CommandManager.AddHandler(Comando, new CommandInfo(OnComando)
        {
            HelpMessage = "Abre ou fecha a caixa de som FFBL.",
        });

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenMainUi += AbrirJanela;
        PluginInterface.UiBuilder.OpenConfigUi += AbrirJanela;

        _ = Task.Run(() => EnviosAsync(ctsEnvios.Token));

        if (Config.ConectarAoAbrir && PodeConectar(out _)) Conectar();
    }

    internal void SalvarConfig() => PluginInterface.SavePluginConfig(Config);

    private void OnDraw()
    {
        Capas.Limpar();
        windowSystem.Draw();
        Dialogos.Draw();
        var telaoParaMim = TelaoEfetivo(Chat.Panel?.Telao);
        // telao de outra pessoa so e exibido se voce nao desligou a opcao (e nunca invoca nada no seu jogo)
        if (telaoParaMim != null && !TelaoPermitido(telaoParaMim)) telaoParaMim = null;
        Minion.Tick(telaoParaMim, Chat.Panel);
        Omne.Tick();
    }

    private void AbrirJanela() => mainWindow.IsOpen = true;

    internal void AlternarJanela() => mainWindow.IsOpen = !mainWindow.IsOpen;

    /// <summary>Mostra ou esconde o icone flutuante (o "macro").</summary>
    internal void DefinirIcone(bool mostrar)
    {
        Config.MostrarIcone = mostrar;
        iconWindow.IsOpen = mostrar;
        SalvarConfig();
    }

    // ------------------------------------------------------------ telao

    /// <summary>Cria o telao a poucos passos a frente do personagem, virado para ele.</summary>
    /// <summary>True se fui eu que criei/movi o telao nesta sessao (so entao ele aparece sem pedir permissao).</summary>
    internal bool MeuTelao { get; private set; }
    /// <summary>Quem criou o telao e o dono. Se o bot ja informou o dono, vale o nome dele (e nao so "eu cliquei antes");
    /// enquanto o bot nao respondeu (previa local), vale o meu clique.</summary>
    internal bool SouDono(TelaoInfo t)
        => string.IsNullOrEmpty(t.Dono)
            ? MeuTelao
            : string.Equals(t.Dono, NomePersonagem() ?? "", StringComparison.OrdinalIgnoreCase);
    internal bool TelaoPermitido(TelaoInfo t) => SouDono(t) || Config.ProjAceitarTelaoDeOutros;

    internal void CriarTelaoAqui(float largura, float altura)
    {
        MeuTelao = true;
        var jogador = ObjectTable.LocalPlayer;
        if (jogador == null) { Chat.SetStatus("Personagem ainda nao carregou.", 3); return; }
        var r = jogador.Rotation;
        var frente = new System.Numerics.Vector3(MathF.Sin(r), 0f, MathF.Cos(r));
        var pos = jogador.Position + frente * 4f;
        var giro = (r + MathF.PI) * 180f / MathF.PI;
        suprimirAte = DateTime.MinValue;
        var novo = new TelaoInfo((uint)ClientState.TerritoryType, pos, largura, altura, giro, "");
        DefinirPrevia(novo, 4.0); // aparece na hora, sem esperar o bot
        EnviarTelao(novo);
    }

    internal void MoverTelaoParaMim(TelaoInfo atual)
    {
        MeuTelao = true;
        var jogador = ObjectTable.LocalPlayer;
        if (jogador == null) return;
        var r = jogador.Rotation;
        var frente = new System.Numerics.Vector3(MathF.Sin(r), 0f, MathF.Cos(r));
        var pos = jogador.Position + frente * 4f;
        var giro = (r + MathF.PI) * 180f / MathF.PI;
        EnviarTelao(new TelaoInfo((uint)ClientState.TerritoryType, pos, atual.Largura, atual.Altura, giro, ""));
    }

    internal void EnviarTelao(TelaoInfo t)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        Enviar(string.Create(c, $"!telao criar {t.Territorio} {t.Pos.X:F2} {t.Pos.Y:F2} {t.Pos.Z:F2} {t.Largura:F1} {t.Altura:F1} {t.Giro:F0}"), ignorarLimite: true);
    }

    // ------------------------------------------------------------ imagens para o chat

    internal string MsgImagem { get; private set; } = "";
    private readonly HashSet<string> arquivosEnviados = new();

    /// <summary>Manda uma imagem escolhida para o chat do Discord (so uma vez por arquivo), se o Discord aceitar.</summary>
    internal void CompartilharImagem(string caminho)
    {
        if (!Conectado || !Webhook.Valido(Config.WebhookUrl)) return;
        if (!Webhook.PodeEnviarArquivo(caminho, out var motivo)) { MsgImagem = $"A imagem nao foi para o chat ({motivo}); so aparece no seu telao."; return; }
        var chave = caminho + "|" + File.GetLastWriteTimeUtc(caminho).Ticks;
        if (!arquivosEnviados.Add(chave)) return;
        var (_, nomeDiscord, avatar) = Chat.Me;
        var nome = NomePersonagem() ?? nomeDiscord;
        var url = Config.WebhookUrl;
        _ = Task.Run(async () =>
        {
            try { await Webhook.EnviarArquivoAsync(url, nome, avatar, "\U0001F5BC " + nome + " exibiu no telao:", caminho); MsgImagem = "Imagem enviada ao chat."; }
            catch (Exception ex) { Log.Warning(ex, "FFBL: nao consegui enviar a imagem"); MsgImagem = "A imagem nao foi para o chat: " + ex.Message; }
        });
    }

    /// <summary>Nome do mapa (ex.: "Mist") a partir do numero do territorio; se nao achar, "mapa N".</summary>
    internal string NomeMapa(uint terr)
    {
        try
        {
            var folha = DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
            if (folha != null && folha.TryGetRow(terr, out var linha))
            {
                var nome = linha.PlaceName.ValueNullable?.Name.ToString();
                if (!string.IsNullOrEmpty(nome)) return nome;
            }
        }
        catch { }
        return "mapa " + terr;
    }

    /// <summary>Saida de emergencia: libera o telao de alguem que saiu do jogo.</summary>
    internal void LiberarTelao()
    {
        MeuTelao = false;
        Previa = null;
        suprimirAte = DateTime.Now.AddSeconds(5);
        Enviar("!telao liberar", ignorarLimite: true);
    }

    internal void RecolherTelao()
    {
        MeuTelao = false;
        Previa = null;
        suprimirAte = DateTime.Now.AddSeconds(5);
        Enviar("!telao recolher", ignorarLimite: true);
    }

    private void OnComando(string command, string args) => mainWindow.IsOpen = !mainWindow.IsOpen;

    /// <summary>Caminho do arquivo de configuracao do OMNE (para copiar Client ID e Secret).</summary>
    internal string CaminhoConfigOmne()
    {
        var pasta = PluginInterface.ConfigDirectory.Parent?.FullName ?? "";
        return Path.Combine(pasta, "OMNE.json");
    }

    // ------------------------------------------------------------ conexao

    internal bool Conectado => tarefa is { IsCompleted: false };

    internal bool PodeConectar(out string motivo)
    {
        if (string.IsNullOrWhiteSpace(Config.ClientId)) { motivo = "Falta o Client ID."; return false; }
        if (string.IsNullOrWhiteSpace(Config.ClientSecret)) { motivo = "Falta o Client Secret."; return false; }
        if (!Webhook.Valido(Config.WebhookUrl)) { motivo = "O link do webhook esta vazio ou invalido."; return false; }
        motivo = "";
        return true;
    }

    internal void Conectar()
    {
        if (Conectado) return;
        if (!PodeConectar(out var motivo))
        {
            Chat.SetStatus(motivo, 3);
            return;
        }
        Chat.Clear();
        cts = new CancellationTokenSource();
        var ct = cts.Token;
        tarefa = Task.Run(() => LoopAsync(ct));
    }

    internal void Desconectar()
    {
        try { cts?.Cancel(); } catch { /* ignora */ }
        Chat.SetStatus("Desconectado", 0);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var canal = await Webhook.ObterCanalAsync(Config.WebhookUrl, ct);
                await rpc.RunAsync(canal, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FFBL: falha na conexao");
                Chat.SetStatus($"{ex.Message} (tentando de novo em 5 s)", 3);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { break; }
        }
        Chat.SetStatus("Desconectado", 0);
    }

    // ------------------------------------------------------------ enviar

    /// <summary>Nome do personagem logado (ou null se ainda nao carregou).</summary>
    internal string? NomePersonagem()
    {
        try { return ObjectTable.LocalPlayer?.Name.TextValue; }
        catch { return null; }
    }

    private void EnviarComo(string texto) => Enviar(texto, ignorarLimite: true);

    /// <summary>Manda um texto para a sala pelo webhook, com o nome do seu personagem e a sua foto.</summary>
    internal void Enviar(string texto, bool ignorarLimite = false)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length == 0) return;
        if (texto.Length > 500) texto = texto[..500];
        if (!Webhook.Valido(Config.WebhookUrl)) { Chat.SetStatus("O link do webhook esta vazio ou invalido.", 3); return; }
        if (!Conectado) { Chat.SetStatus("Conecte primeiro.", 3); return; }

        var agora = DateTime.UtcNow;
        if (!ignorarLimite && (agora - ultimoEnvio).TotalMilliseconds < 600) return; // evita rajada
        ultimoEnvio = agora;

        var (_, nomeDiscord, avatar) = Chat.Me;
        var nome = NomePersonagem() ?? nomeDiscord;
        envios.Writer.TryWrite((Config.WebhookUrl, nome, avatar, texto));
    }

    /// <summary>Envia uma mensagem de cada vez, em ordem, sem estourar o limite do Discord.</summary>
    private async Task EnviosAsync(CancellationToken ct)
    {
        var leitor = envios.Reader;
        try
        {
            while (await leitor.WaitToReadAsync(ct))
            {
                while (leitor.TryRead(out var m))
                {
                    try { await Webhook.EnviarAsync(m.Url, m.Nome, m.Avatar, m.Texto, ct); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { Log.Warning(ex, "FFBL: falha ao enviar"); } // so no registro, sem aviso na tela
                    await Task.Delay(350, ct);
                }
            }
        }
        catch (OperationCanceledException) { /* fechando */ }
    }

    // ------------------------------------------------------------ fim

    public void Dispose()
    {
        try { Minion.Dispose(); } catch { /* ignora */ }
        try { Omne.Dispose(); } catch { /* ignora */ }
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenMainUi -= AbrirJanela;
        PluginInterface.UiBuilder.OpenConfigUi -= AbrirJanela;
        CommandManager.RemoveHandler(Comando);
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        try { ctsEnvios.Cancel(); } catch { /* ignora */ }
        try { cts?.Cancel(); } catch { /* ignora */ }
        try { tarefa?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignora */ }
        cts?.Dispose();
    }
}
