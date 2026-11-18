using System.Numerics;
using System.Text.Json.Nodes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FFBL;

/// <summary>O "iPod": uma imagem de fundo com a tela e a roda desenhadas por cima.</summary>
internal sealed class MainWindow : Window, IDisposable
{
    // Medidas na imagem original (939 x 1556)
    private const float ImgL = 939f, ImgA = 1556f;
    private static readonly Vector4 Tela = new(102, 107, 834, 694);      // x0, y0, x1, y1 da tela preta
    private static readonly Vector4 Cabecalho = new(115, 119, 475, 171);
    private static readonly Vector4 BarraAzul = new(115, 173, 475, 216);
    private static readonly Vector4 Lista = new(115, 218, 475, 688);
    private static readonly Vector4 Capa = new(479, 119, 829, 640);
    private static readonly Vector4 BarraVolume = new(479, 646, 829, 688);
    private static readonly Vector4 ZMenu = new(385, 830, 555, 905);
    private static readonly Vector4 ZAnterior = new(195, 1040, 330, 1155);
    private static readonly Vector4 ZProxima = new(610, 1040, 745, 1155);
    private static readonly Vector4 ZPlay = new(405, 1290, 535, 1365);
    private static readonly Vector2 CentroBotao = new(466, 1094);
    private const float RaioBotao = 104f;
    private static readonly Vector4 ZFechar = new(731, 1348, 782, 1399);
    private static readonly Vector4 ZVolMais = new(142, 748, 190, 796);
    private static readonly Vector4 ZVolMenos = new(142, 814, 190, 862);

    private enum Aba { Conectar, Playlist }

    private readonly Plugin plugin;
    private bool estilo;

    private Aba aba = Aba.Conectar;
    private bool pedindo;
    private bool focarPedido;
    private string pedido = "";
    private int rolagem;

    // telao (valores dos controles)
    private float telaoLargura = 4f, telaoAltura = 3f, telaoGiro = 0f;
    private float empDist, empLado, empAlt; // "empurrar": distancia, lado e altura (relativos)
    private Vector3 telaoBase;
    private bool telaoAtivo;
    private DateTime telaoEnviadoEm = DateTime.MinValue;

    private string clientId = "", clientSecret = "", webhook = "", avisoConfig = "", codigoConexao = "";

    public MainWindow(Plugin plugin) : base("FFBL###FFBL_Main")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;
        RespectCloseHotkey = false;
        clientId = plugin.Config.ClientId;
        clientSecret = plugin.Config.ClientSecret;
        webhook = plugin.Config.WebhookUrl;
    }

    private float Esc => Math.Clamp(plugin.Config.Escala, 0.3f, 1.2f);

    public override void PreDraw()
    {
        var e = Esc;
        Size = new Vector2(ImgL * e, ImgA * e);
        SizeCondition = ImGuiCond.Always;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        estilo = true;
    }

    public override void PostDraw()
    {
        if (estilo) { ImGui.PopStyleVar(); estilo = false; }
    }

    // ------------------------------------------------------------ utilidades de desenho

    private Vector2 origem;
    private float sc;

    private Vector2 P(float x, float y) => origem + new Vector2(x * sc, y * sc);
    private Vector2 Pmin(Vector4 r) => P(r.X, r.Y);
    private Vector2 Pmax(Vector4 r) => P(r.Z, r.W);

    private static uint Cor(float r, float g, float b, float a = 1f) => ImGui.GetColorU32(new Vector4(r, g, b, a));

    /// <summary>Texto que, se nao couber, anda para um lado e para o outro (efeito boomerang).</summary>
    private static void TextoBoomerang(ImDrawListPtr dl, string texto, Vector2 pos, float largura, uint cor)
    {
        var tam = ImGui.CalcTextSize(texto);
        var altura = tam.Y + 2;
        float desloc = 0;
        if (tam.X > largura)
        {
            var sobra = tam.X - largura;
            const float velocidade = 40f; // pixels por segundo
            const float pausa = 1.2f;     // segundos parado em cada ponta
            var ida = sobra / velocidade;
            var ciclo = 2 * (pausa + ida);
            var t = (float)(ImGui.GetTime() % ciclo);
            if (t < pausa) desloc = 0;
            else if (t < pausa + ida) desloc = (t - pausa) * velocidade;
            else if (t < 2 * pausa + ida) desloc = sobra;
            else desloc = sobra - (t - (2 * pausa + ida)) * velocidade;
        }
        dl.PushClipRect(pos, new Vector2(pos.X + largura, pos.Y + altura), true);
        dl.AddText(new Vector2(pos.X - desloc, pos.Y), cor, texto);
        dl.PopClipRect();
    }

    private static void TextoCentralizado(ImDrawListPtr dl, string texto, Vector2 centro, uint cor)
    {
        var tam = ImGui.CalcTextSize(texto);
        dl.AddText(new Vector2(centro.X - tam.X / 2, centro.Y - tam.Y / 2), cor, texto);
    }

    private bool Zona(string id, Vector4 r, string dica)
    {
        ImGui.SetCursorScreenPos(Pmin(r));
        var clicou = ImGui.InvisibleButton(id, new Vector2((r.Z - r.X) * sc, (r.W - r.Y) * sc));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(dica);
        return clicou;
    }

    // ------------------------------------------------------------ quadro

    public override void Draw()
    {
        sc = Esc;
        origem = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        if (!plugin.Conectado && aba == Aba.Playlist) { aba = Aba.Conectar; pedindo = false; }

        // fundo: a imagem do iPod
        var caminho = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? "", "images", "ipod.png");
        var fundo = Plugin.TextureProvider.GetFromFile(caminho).GetWrapOrEmpty();
        dl.AddImage(fundo.Handle, origem, origem + new Vector2(ImgL * sc, ImgA * sc));

        if (aba == Aba.Conectar) DesenharConectar();
        else DesenharPlaylist(dl);

        DesenharAviso(dl);
        DesenharRoda(dl);
    }

    // ------------------------------------------------------------ tela de conectar

    private void DesenharConectar()
    {
        ImGui.SetCursorScreenPos(Pmin(Tela));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.13f, 0.13f, 0.13f, 1f));
        ImGui.BeginChild("##tela_conectar", new Vector2((Tela.Z - Tela.X) * sc, (Tela.W - Tela.Y) * sc), false);
        ImGui.Indent(10);
        ImGui.Dummy(new Vector2(1, 4));
        ImGui.PushTextWrapPos(0); // textos longos quebram a linha em vez de sair da tela

        if (ImGui.BeginTabBar("##abas"))
        {
            if (ImGui.BeginTabItem("FFBL"))
            {
        var (texto, tipo) = plugin.Chat.Status;
        var cor = tipo switch { 1 => new Vector4(1f, 0.85f, 0.3f, 1f), 2 => new Vector4(0.4f, 1f, 0.5f, 1f), 3 => new Vector4(1f, 0.4f, 0.4f, 1f), _ => new Vector4(0.7f, 0.7f, 0.7f, 1f) };

        if (plugin.Conectado)
        {
            if (ImGui.Button(Loc.T("Desconectar"))) plugin.Desconectar();
        }
        else if (ImGui.Button(Loc.T("Conectar")))
        {
            plugin.Config.ClientId = clientId.Trim();
            plugin.Config.ClientSecret = clientSecret.Trim();
            plugin.Config.WebhookUrl = webhook.Trim();
            plugin.SalvarConfig();
            plugin.Conectar();
        }
        ImGui.SameLine();
        ImGui.PushTextWrapPos(0);
        ImGui.TextColored(cor, Loc.T(texto));
        ImGui.PopTextWrapPos();

        if (plugin.Conectado && plugin.Chat.Status.Kind == 2)
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), Loc.T("Aperte MENU na roda para abrir a playlist."));

        ImGui.Separator();

        if (File.Exists(plugin.CaminhoConfigOmne()) && ImGui.Button(Loc.T("Copiar Client ID e Secret do OMNE")))
        {
            try
            {
                var no = JsonNode.Parse(File.ReadAllText(plugin.CaminhoConfigOmne()));
                clientId = (string?)no?["ClientId"] ?? clientId;
                clientSecret = (string?)no?["ClientSecret"] ?? clientSecret;
                avisoConfig = "Copiei do OMNE.";
            }
            catch (Exception ex) { avisoConfig = "Nao consegui ler o OMNE: " + ex.Message; }
        }

        var largura = (Tela.Z - Tela.X) * sc - 150;

        ImGui.Text(Loc.T("Codigo de conexao (quem criou a sala te passa)"));
        ImGui.SetNextItemWidth(largura);
        ImGui.InputText("##codigoconexao", ref codigoConexao, 512);
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Aplicar codigo")))
        {
            if (CodigoConexao.TentarLer(codigoConexao, out var d, out var erroCodigo) && d != null)
            {
                clientId = d.ClientId; clientSecret = d.ClientSecret; webhook = d.WebhookUrl;
                plugin.Config.ClientId = clientId; plugin.Config.ClientSecret = clientSecret; plugin.Config.WebhookUrl = webhook;
                plugin.Config.Omne.ClientId = clientId; plugin.Config.Omne.ClientSecret = clientSecret;
                plugin.Config.Omne.ChannelId = d.SalaId;
                plugin.SalvarConfig();
                codigoConexao = "";
                avisoConfig = "Codigo aplicado. Agora clique em Conectar.";
            }
            else avisoConfig = erroCodigo;
        }

        if (ImGui.CollapsingHeader(Loc.T("Preencher a mao (avancado)") + "###maoconfig"))
        {
        ImGui.SetNextItemWidth(largura);
        ImGui.InputText("Client ID", ref clientId, 64);
        ImGui.SetNextItemWidth(largura);
        ImGui.InputText("Client Secret", ref clientSecret, 128, ImGuiInputTextFlags.Password);
        ImGui.SetNextItemWidth(largura);
        ImGui.InputText("Webhook", ref webhook, 256, ImGuiInputTextFlags.Password);
        }

        var auto = plugin.Config.ConectarAoAbrir;
        if (ImGui.Checkbox(Loc.T("Conectar sozinho ao abrir o jogo"), ref auto))
        {
            plugin.Config.ConectarAoAbrir = auto;
            plugin.SalvarConfig();
        }

        var esc = plugin.Config.Escala;
        ImGui.Text(Loc.T("Tamanho do iPod"));
        ImGui.SameLine();
        if (ImGui.Button("-##esc")) { esc = MathF.Round(Math.Clamp(esc - 0.05f, 0.3f, 1.0f), 2); plugin.Config.Escala = esc; plugin.SalvarConfig(); }
        ImGui.SameLine();
        ImGui.Text(esc.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        ImGui.SameLine();
        if (ImGui.Button("+##esc")) { esc = MathF.Round(Math.Clamp(esc + 0.05f, 0.3f, 1.0f), 2); plugin.Config.Escala = esc; plugin.SalvarConfig(); }

        ImGui.Separator();
        var mostrar = plugin.Config.MostrarIcone;
        if (ImGui.Button(mostrar ? Loc.T("Macro: esconder o icone") : Loc.T("Macro: mostrar o icone")))
            plugin.DefinirIcone(!mostrar);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), Loc.T("(icone para abrir o FFBL)"));

        ImGui.Separator();

        if (ImGui.Button(Loc.T("Esquecer autorizacao")))
        {
            plugin.Config.AccessToken = "";
            plugin.SalvarConfig();
            avisoConfig = "Na proxima conexao o Discord pede autorizacao de novo.";
        }
        if (avisoConfig.Length > 0)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), avisoConfig);
            ImGui.PopTextWrapPos();
        }
        ImGui.PushTextWrapPos(0);
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), Loc.T("O link do webhook funciona como senha: nao mostre nem compartilhe. No Discord, seu nome aparece como o do seu personagem."));
        ImGui.PopTextWrapPos();

                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(Loc.T("Projecao") + "###tabproj"))
            {
                DesenharProjecao();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("OMNE"))
            {
                plugin.Omne.Desenhar((Tela.Z - Tela.X) * sc - 40);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(Loc.T("Idioma") + "###tabidioma"))
            {
                ImGui.Text(Loc.T("Idioma do FFBL"));
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), Loc.T("Escolha o idioma dos textos do plugin."));
                foreach (var (codigo, nome) in Loc.Idiomas)
                {
                    if (ImGui.RadioButton(nome, Loc.Idioma == codigo))
                    {
                        Loc.Idioma = codigo;
                        plugin.Config.Idioma = codigo;
                        plugin.SalvarConfig();
                    }
                }
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), Loc.T("Textos vindos do bot e do Discord continuam no idioma original."));
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        ImGui.PopTextWrapPos();
        ImGui.Unindent(10);
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private bool linAtiva, linSoltou, linClicou, envioPendente;
    private DateTime envioQuando = DateTime.MinValue;

    /// <summary>Uma linha de ajuste: barra, botoes - e + (de 0.05 em 0.05) e campo para digitar. Devolve true se o valor mudou.</summary>
    private bool Linha(string rotulo, ref float v, float min, float max, float limMin, float limMax, float larg)
    {
        const float passo = 0.05f;
        const float bw = 22f, iw = 66f;
        var sp = ImGui.GetStyle().ItemSpacing.X;
        var sw = Math.Max(larg - iw - bw * 2 - sp * 4 - 50, 40f);
        var antes = v;

        ImGui.SetNextItemWidth(sw);
        ImGui.SliderFloat("##s" + rotulo, ref v, min, max, "%.2f");
        linAtiva |= ImGui.IsItemActive();
        linSoltou |= ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SameLine();
        if (ImGui.Button("-##m" + rotulo, new Vector2(bw, 0))) { v -= passo; linClicou = true; }
        ImGui.SameLine();
        if (ImGui.Button("+##p" + rotulo, new Vector2(bw, 0))) { v += passo; linClicou = true; }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(iw);
        ImGui.InputFloat("##i" + rotulo, ref v);
        linAtiva |= ImGui.IsItemActive();
        linSoltou |= ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SameLine();
        ImGui.TextUnformatted(rotulo);

        v = Math.Clamp(MathF.Round(v, 3), limMin, limMax);
        return MathF.Abs(v - antes) > 0.0001f;
    }

    private void DesenharMinion(Vector4 cinza)
    {
        var t = plugin.Minion;
        var c = plugin.Config;
        ImGui.Text(Loc.T("Telao no mundo (seu Plush Cushion)"));
        ImGui.TextColored(cinza, Loc.T("Ao criar o telao, o FFBL invoca o Plush Cushion e ele vira a tela. Precisa do Penumbra."));
        ImGui.TextWrapped(string.IsNullOrEmpty(t.Estado) ? "..." : Loc.T(t.Estado));

        if (ImGui.TreeNode(Loc.T("Ajuste da imagem na tela")))
        {
            var larg = (Tela.Z - Tela.X) * sc - 150;
            var tst = c.ProjTeste;
            if (ImGui.Checkbox(Loc.T("Mostrar grade de teste"), ref tst)) { c.ProjTeste = tst; plugin.SalvarConfig(); }
            ImGui.TextColored(cinza, Loc.T("Use se a imagem aparecer cortada ou deslocada."));
            var dx = c.ProjImgDx; var dy = c.ProjImgDy; var ex = c.ProjImgEx; var ey = c.ProjImgEy;
            var ch = false;
            var bri = c.ProjBrilho;
            if (Linha(Loc.T("Brilho"), ref bri, 0f, 1f, 0f, 4f, larg)) { c.ProjBrilho = bri; plugin.SalvarConfig(); }
            ImGui.TextColored(cinza, Loc.T("Brilho: luz propria da tela, para os dois lados ficarem claros."));
            ch |= Linha(Loc.T("Mover X"), ref dx, -1f, 1f, -1f, 1f, larg);
            ch |= Linha(Loc.T("Mover Y"), ref dy, -1f, 1f, -1f, 1f, larg);
            ch |= Linha(Loc.T("Zoom X"), ref ex, 0.2f, 3f, 0.05f, 5f, larg);
            ch |= Linha(Loc.T("Zoom Y"), ref ey, 0.2f, 3f, 0.05f, 5f, larg);
            if (ch) { c.ProjImgDx = dx; c.ProjImgDy = dy; c.ProjImgEx = ex; c.ProjImgEy = ey; plugin.SalvarConfig(); }
            if (ImGui.Button(Loc.T("Zerar ajuste"))) { c.ProjImgDx = 0; c.ProjImgDy = 0; c.ProjImgEx = 1; c.ProjImgEy = 1; plugin.SalvarConfig(); }
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(Loc.T("Detalhes tecnicos e ajuste fino")))
        {
            var bl = c.ProjBaseLargura; var ba = c.ProjBaseAltura; var gm = c.ProjGiroModelo;
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("Largura natural da parede", ref bl)) { c.ProjBaseLargura = Math.Clamp(bl, 0.1f, 50f); plugin.SalvarConfig(); }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("Altura natural da parede", ref ba)) { c.ProjBaseAltura = Math.Clamp(ba, 0.1f, 50f); plugin.SalvarConfig(); }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("Giro do modelo (graus)", ref gm)) { c.ProjGiroModelo = gm; plugin.SalvarConfig(); }
            var eh = c.ProjEspelharH; var ev = c.ProjEspelharV;
            if (ImGui.Checkbox(Loc.T("Virar imagem na horizontal (alem do automatico)"), ref eh)) { c.ProjEspelharH = eh; plugin.SalvarConfig(); }
            if (ImGui.Checkbox(Loc.T("Virar imagem na vertical"), ref ev)) { c.ProjEspelharV = ev; plugin.SalvarConfig(); }
            var cp = c.ProjCostasPretas;
            if (ImGui.Checkbox(Loc.T("Lado de tras preto (pode piscar ao dar a volta)"), ref cp)) { c.ProjCostasPretas = cp; plugin.SalvarConfig(); }
            if (ImGui.Button(Loc.T("Reaplicar"))) t.Reaplicar();
            ImGui.TextColored(cinza, Loc.T("Penumbra: ") + Loc.T(t.PenumbraOk ? "respondeu" : "nao respondeu"));
            ImGui.PushTextWrapPos(0);
            foreach (var linha in t.Diario) ImGui.TextColored(cinza, linha);
            ImGui.PopTextWrapPos();
            ImGui.TreePop();
        }
    }

    private void DesenharProjecao()
    {
        var cinza = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var painel = plugin.Chat.Panel;
        var telao = plugin.TelaoEfetivo(painel?.Telao);

        ImGui.Text(Loc.T("Telao"));

        var aceitar = plugin.Config.ProjAceitarTelaoDeOutros;
        if (ImGui.Checkbox(Loc.T("Ver o telao criado por outras pessoas") + "##aceitartelao", ref aceitar))
        {
            plugin.Config.ProjAceitarTelaoDeOutros = aceitar;
            plugin.SalvarConfig();
        }
        ImGui.TextColored(cinza, Loc.T("O telao aparece no minion de quem o criou. O FFBL nao invoca nada no seu jogo."));

        if (telao != null && !plugin.SouDono(telao))
        {
            // so existe um telao por vez: quem nao o criou so ve o aviso (nao mexe no telao dos outros)
            ImGui.Spacing();
            var dono = string.IsNullOrEmpty(telao.Dono) ? "?" : telao.Dono;
            ImGui.TextWrapped(string.Format(Loc.T("Ja existe um telao sendo usado, invocado por {0} em {1} (X {2:F1}, Z {3:F1})."),
                dono, plugin.NomeMapa(telao.Territorio), telao.Pos.X, telao.Pos.Z));
            if (!plugin.TelaoPermitido(telao))
                ImGui.TextColored(cinza, Loc.T("Ele nao aparece no seu jogo porque voce desligou a opcao acima."));
            ImGui.TextColored(cinza, Loc.T("Se essa pessoa saiu do jogo, voce pode liberar o telao."));
            if (ImGui.Button(Loc.T("Liberar telao") + "##liberartelao")) plugin.LiberarTelao();
            return;
        }

        if (telao == null)
        {
            if (!plugin.Conectado) ImGui.TextColored(cinza, Loc.T("Conecte primeiro para criar um telao."));
            else if (ImGui.Button(Loc.T("Criar telao aqui"))) plugin.CriarTelaoAqui(telaoLargura, telaoAltura);
        }
        else
        {
            // enquanto ninguem mexe, os controles seguem o que o bot informou
            if (!telaoAtivo && (DateTime.Now - telaoEnviadoEm).TotalSeconds > 2.5)
            {
                telaoLargura = telao.Largura;
                telaoAltura = telao.Altura;
                telaoGiro = telao.Giro;
                telaoBase = telao.Pos;
                empDist = empLado = empAlt = 0f;
            }

            var largura = (Tela.Z - Tela.X) * sc - 150;
            linAtiva = linSoltou = linClicou = false;

            Linha(Loc.T("Largura"), ref telaoLargura, 1f, 20f, 1f, 60f, largura);
            Linha(Loc.T("Altura"), ref telaoAltura, 1f, 15f, 1f, 40f, largura);
            Linha(Loc.T("Giro"), ref telaoGiro, 0f, 360f, 0f, 360f, largura);
            Linha(Loc.T("Dist."), ref empDist, -20f, 20f, -200f, 200f, largura);   // + empurra para longe de quem olha
            Linha(Loc.T("Lado"), ref empLado, -20f, 20f, -200f, 200f, largura);    // + vai para a direita de quem olha
            Linha(Loc.T("Subir"), ref empAlt, -10f, 10f, -100f, 100f, largura);

            var ativo = linAtiva;
            var soltou = linSoltou;
            // clique em + ou -: espera um instante para juntar varios cliques num so envio
            if (linClicou) { envioPendente = true; envioQuando = DateTime.Now.AddSeconds(0.7); }
            if (envioPendente && !ativo && DateTime.Now >= envioQuando) { soltou = true; envioPendente = false; }
            telaoAtivo = ativo || envioPendente;
            ativo |= envioPendente;

            var ang = telaoGiro * MathF.PI / 180f;
            var frente = new Vector3(MathF.Sin(ang), 0f, MathF.Cos(ang));
            var direita = new Vector3(MathF.Cos(ang), 0f, -MathF.Sin(ang));
            var pos = telaoBase - frente * empDist + direita * empLado + Vector3.UnitY * empAlt;
            var efetivo = new TelaoInfo(telao.Territorio, pos, telaoLargura, telaoAltura, telaoGiro, telao.Dono);

            if (ativo) plugin.DefinirPrevia(efetivo, 0.4); // mostra a mudanca na hora, so para voce
            if (soltou)
            {
                telaoEnviadoEm = DateTime.Now;
                plugin.EnviarTelao(efetivo);
                plugin.DefinirPrevia(efetivo, 3.0);
            }

            if (ImGui.Button(Loc.T("Mover para onde estou")))
            {
                telaoEnviadoEm = DateTime.Now;
                plugin.MoverTelaoParaMim(efetivo);
            }
            ImGui.SameLine();
            if (ImGui.Button(Loc.T("Recolher tela"))) plugin.RecolherTelao();
            if (ImGui.Button(Loc.T("Inverter frente e verso")))
            {
                var g = plugin.Config.ProjGiroModelo + 180f;
                plugin.Config.ProjGiroModelo = g >= 360f ? g - 360f : g;
                plugin.SalvarConfig();
            }
            ImGui.SameLine();
            ImGui.TextColored(cinza, plugin.Config.ProjGiroModelo >= 90f && plugin.Config.ProjGiroModelo < 270f ? Loc.T("(invertido)") : Loc.T("(normal)"));
            ImGui.TextColored(cinza, Loc.T("Telao de: ") + telao.Dono);

            ImGui.Text(Loc.T("Ajustes so no seu jogo"));
            var cfg = plugin.Config;
            var rx = cfg.ProjRotX; var rz = cfg.ProjRotZ; var ez = cfg.ProjEspessura;
            var mudou = false;
            mudou |= Linha(Loc.T("Rot X"), ref rx, -180f, 180f, -180f, 180f, largura);
            mudou |= Linha(Loc.T("Rot Z"), ref rz, -180f, 180f, -180f, 180f, largura);
            mudou |= Linha(Loc.T("Esp. Z"), ref ez, 0.1f, 5f, 0.01f, 20f, largura);
            if (mudou)
            {
                cfg.ProjRotX = rx;
                cfg.ProjRotZ = rz;
                cfg.ProjEspessura = ez;
                plugin.SalvarConfig();
            }
        }

        ImGui.Separator();
        DesenharMinion(cinza);

        ImGui.Separator();
        ImGui.Text(Loc.T("O que mostrar no telao"));
        ImGui.TextColored(cinza, Loc.T("Vale so para voce: cada pessoa escolhe o que ve."));

        void Opcao(string rotulo, int valor)
        {
            if (ImGui.RadioButton(rotulo, plugin.Config.ProjFonte == valor))
            {
                plugin.Config.ProjFonte = valor;
                plugin.SalvarConfig();
            }
        }

        Opcao(Loc.T("Imagem padrao (FFBL)"), 0);
        Opcao(Loc.T("Capa da musica atual"), 1);
        Opcao(Loc.T("Uma imagem do meu computador"), 2);
        if (plugin.Config.ProjFonte == 2)
        {
            if (ImGui.Button(Loc.T("Procurar imagem...")))
            {
                plugin.Dialogos.OpenFileDialog("Escolha uma imagem", "Imagens{.png,.PNG,.jpg,.JPG,.jpeg,.JPEG,.bmp,.BMP,.webp,.WEBP,.tif,.TIF,.tiff,.TIFF}", (ok, caminho) =>
                {
                    if (!ok || string.IsNullOrEmpty(caminho)) return;
                    plugin.Config.ProjImagemLocal = caminho;
                    plugin.SalvarConfig();
                    plugin.CompartilharImagem(caminho); // a imagem tambem vai para o chat do Discord (ate 10 MB)
                });
            }
            var arq = plugin.Config.ProjImagemLocal ?? "";
            ImGui.TextColored(cinza, string.IsNullOrEmpty(arq) ? Loc.T("(nenhuma imagem escolhida: mostra a imagem padrao)") : arq);
            ImGui.TextColored(cinza, Loc.T("A imagem tambem vai para o chat do Discord (ate 10 MB), para quem nao tem o FFBL."));
        }
        if (!string.IsNullOrEmpty(plugin.MsgImagem)) ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), plugin.MsgImagem);
    }

    // ------------------------------------------------------------ tela da playlist

    private void DesenharPlaylist(ImDrawListPtr dl)
    {
        var painel = plugin.Chat.Panel;
        var estado = painel?.Estado ?? Loc.T("Parado");
        var titulo = painel?.Titulo ?? "";
        var fila = painel?.Fila ?? new List<string>();

        // fundo da tela
        dl.AddRectFilled(Pmin(Tela), Pmax(Tela), Cor(0.13f, 0.13f, 0.13f));

        // ---- esquerda: cabecalho, barra azul, lista
        dl.AddRectFilled(Pmin(Cabecalho), Pmax(Cabecalho), Cor(0.07f, 0.07f, 0.08f));
        TextoCentralizado(dl, Loc.T(estado), (Pmin(Cabecalho) + Pmax(Cabecalho)) / 2, Cor(0.85f, 0.85f, 0.88f));


        var bMin = Pmin(BarraAzul);
        var bMax = Pmax(BarraAzul);
        dl.AddRectFilledMultiColor(bMin, bMax, Cor(0.35f, 0.62f, 0.95f), Cor(0.35f, 0.62f, 0.95f), Cor(0.10f, 0.32f, 0.72f), Cor(0.10f, 0.32f, 0.72f));
        var altTexto = ImGui.GetTextLineHeight();
        var textoBarra = string.IsNullOrEmpty(titulo) ? "Nada tocando" : titulo;
        TextoBoomerang(dl, textoBarra, new Vector2(bMin.X + 8, bMin.Y + (bMax.Y - bMin.Y - altTexto) / 2), bMax.X - bMin.X - 36, Cor(1f, 1f, 1f));
        dl.AddText(new Vector2(bMax.X - 20, bMin.Y + (bMax.Y - bMin.Y - altTexto) / 2), Cor(1f, 1f, 1f), ">");

        var lMin = Pmin(Lista);
        var lMax = Pmax(Lista);
        dl.AddRectFilled(lMin, lMax, Cor(0.93f, 0.90f, 0.84f));
        var alturaLinha = Math.Max(altTexto + 10, 34f * sc);
        var visiveis = Math.Max(1, (int)((lMax.Y - lMin.Y) / alturaLinha));

        ImGui.SetCursorScreenPos(lMin);
        ImGui.InvisibleButton("##lista", lMax - lMin);
        if (ImGui.IsItemHovered())
        {
            var roda = ImGui.GetIO().MouseWheel;
            if (roda > 0) rolagem--;
            else if (roda < 0) rolagem++;
        }
        rolagem = Math.Clamp(rolagem, 0, Math.Max(0, fila.Count - visiveis));

        if (fila.Count == 0)
        {
            TextoCentralizado(dl, Loc.T("Fila vazia"), (lMin + lMax) / 2, Cor(0.45f, 0.40f, 0.35f));
        }
        else
        {
            for (var i = 0; i < visiveis && rolagem + i < fila.Count; i++)
            {
                var y = lMin.Y + i * alturaLinha;
                dl.AddLine(new Vector2(lMin.X + 6, y + alturaLinha), new Vector2(lMax.X - 6, y + alturaLinha), Cor(0.75f, 0.70f, 0.62f), 1f);
                TextoBoomerang(dl, fila[rolagem + i], new Vector2(lMin.X + 10, y + (alturaLinha - altTexto) / 2), lMax.X - lMin.X - 20, Cor(0.22f, 0.16f, 0.12f));
            }
            if (fila.Count > visiveis)
            {
                var pag = $"{rolagem + 1}/{fila.Count}";
                var hMin = Pmin(Cabecalho);
                var hMax = Pmax(Cabecalho);
                dl.AddText(new Vector2(hMax.X - ImGui.CalcTextSize(pag).X - 8, hMin.Y + (hMax.Y - hMin.Y - altTexto) / 2), Cor(0.6f, 0.75f, 1f), pag);
            }
        }

        // ---- direita: capa
        var cMin = Pmin(Capa);
        var cMax = Pmax(Capa);
        dl.AddRectFilled(cMin, cMax, Cor(0.05f, 0.05f, 0.06f));
        var tex = plugin.Capas.Obter(painel?.Thumb ?? "");
        if (tex != null && tex.Width > 0 && tex.Height > 0)
        {
            // "cobrir": recorta o centro da imagem para preencher a moldura
            var alvo = (cMax.X - cMin.X) / (cMax.Y - cMin.Y);
            var imagem = (float)tex.Width / tex.Height;
            Vector2 uv0 = Vector2.Zero, uv1 = Vector2.One;
            if (imagem > alvo) { var f = alvo / imagem; uv0.X = (1 - f) / 2; uv1.X = 1 - uv0.X; }
            else { var f = imagem / alvo; uv0.Y = (1 - f) / 2; uv1.Y = 1 - uv0.Y; }
            dl.AddImage(tex.Handle, cMin, cMax, uv0, uv1);
        }
        else
        {
            TextoCentralizado(dl, string.IsNullOrEmpty(titulo) ? "FFBL" : Loc.T("sem capa"), (cMin + cMax) / 2, Cor(0.5f, 0.5f, 0.55f));
        }
        if (estado == "Pausado")
        {
            dl.AddRectFilled(new Vector2(cMin.X, cMax.Y - altTexto - 14), cMax, Cor(0f, 0f, 0f, 0.6f));
            TextoCentralizado(dl, Loc.T("Pausado"), new Vector2((cMin.X + cMax.X) / 2, cMax.Y - altTexto / 2 - 7), Cor(1f, 1f, 1f));
        }

        // ---- barra de volume (igual a da musica atual)
        var vMin = Pmin(BarraVolume);
        var vMax = Pmax(BarraVolume);
        var volume = plugin.Config.VolumePessoal;
        dl.AddRectFilledMultiColor(vMin, vMax, Cor(0.35f, 0.62f, 0.95f), Cor(0.35f, 0.62f, 0.95f), Cor(0.10f, 0.32f, 0.72f), Cor(0.10f, 0.32f, 0.72f));
        var nivel = Math.Clamp(volume / 200f, 0f, 1f);
        dl.AddRectFilled(vMin, new Vector2(vMin.X + (vMax.X - vMin.X) * nivel, vMax.Y), Cor(1f, 1f, 1f, 0.28f));
        TextoCentralizado(dl, $"Volume {volume}%", (vMin + vMax) / 2, Cor(1f, 1f, 1f));

        // ---- pedir musica (botao central)
        if (pedindo) DesenharPedido(dl);
    }

    private void DesenharPedido(ImDrawListPtr dl)
    {
        var tMin = Pmin(Tela);
        var tMax = Pmax(Tela);
        dl.AddRectFilled(tMin, tMax, Cor(0f, 0f, 0f, 0.82f));
        var centro = (tMin + tMax) / 2;
        TextoCentralizado(dl, Loc.T("Nome da musica ou link do YouTube:"), centro - new Vector2(0, 36), Cor(1f, 1f, 1f));
        TextoCentralizado(dl, Loc.T("ENTER = pedir     ESC = cancelar"), centro + new Vector2(0, 40), Cor(0.7f, 0.7f, 0.7f));

        var largura = (tMax.X - tMin.X) - 40;
        ImGui.SetCursorScreenPos(new Vector2(tMin.X + 20, centro.Y - 10));
        ImGui.SetNextItemWidth(largura);
        if (focarPedido) { ImGui.SetKeyboardFocusHere(); focarPedido = false; }
        var enter = ImGui.InputText("##pedido", ref pedido, 300, ImGuiInputTextFlags.EnterReturnsTrue);
        if (enter)
        {
            if (pedido.Trim().Length > 0) plugin.Enviar("!tocar " + pedido.Trim());
            pedido = "";
            pedindo = false;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            pedido = "";
            pedindo = false;
        }
    }

    // ------------------------------------------------------------ aviso (respostas do bot, erros)

    private void DesenharAviso(ImDrawListPtr dl)
    {
        string texto = "";
        uint cor = Cor(1f, 1f, 1f);
        var (st, tipo) = plugin.Chat.Status;
        var (resp, quando) = plugin.Chat.LastReply;

        if (aba == Aba.Playlist)
        {
            if (resp.Length > 0 && (DateTime.Now - quando).TotalSeconds < 5) texto = resp;
            if (plugin.MsgVolume.Length > 0) texto = plugin.MsgVolume;
        }
        if (texto.Length == 0) return;

        var tMin = Pmin(Tela);
        var tMax = Pmax(Tela);
        var h = ImGui.GetTextLineHeight() + 12;
        var min = new Vector2(tMin.X + 8, tMax.Y - h - 8);
        var max = new Vector2(tMax.X - 8, tMax.Y - 8);
        dl.AddRectFilled(min, max, Cor(0f, 0f, 0f, 0.8f), 6f);
        TextoBoomerang(dl, texto, new Vector2(min.X + 8, min.Y + 6), max.X - min.X - 16, cor);
    }

    // ------------------------------------------------------------ roda e botoes

    private void DesenharRoda(ImDrawListPtr dl)
    {
        // fechar: o icone desenhado no canto inferior direito da imagem (faz o mesmo que /ffbl)
        if (Zona("##fechar", ZFechar, "Fechar (volte com /ffbl)")) IsOpen = false;

        if (Zona("##volmais", ZVolMais, "Volume +")) plugin.AjustarVolumePessoal(+10);
        if (Zona("##volmenos", ZVolMenos, "Volume -")) plugin.AjustarVolumePessoal(-10);

        if (Zona("##menu", ZMenu, "MENU: alterna entre conectar e playlist"))
        {
            pedindo = false;
            if (aba == Aba.Playlist) aba = Aba.Conectar;
            else if (plugin.Conectado) aba = Aba.Playlist;
            else plugin.Chat.SetStatus("Conecte primeiro para abrir a playlist.", 3);
        }

        if (Zona("##anterior", ZAnterior, "Voltar: recoloca na fila a ultima musica que tocou")) plugin.Enviar("!voltar");
        if (Zona("##proxima", ZProxima, "Proxima: pula a musica")) plugin.Enviar("!pular");
        if (Zona("##play", ZPlay, "Tocar / pausar")) plugin.Enviar("!alternar");

        // botao central: so conta o clique dentro do circulo
        var zc = new Vector4(CentroBotao.X - RaioBotao, CentroBotao.Y - RaioBotao, CentroBotao.X + RaioBotao, CentroBotao.Y + RaioBotao);
        var clicouQuadrado = Zona("##central", zc, "Pedir uma musica");
        if (clicouQuadrado)
        {
            var mouse = ImGui.GetIO().MousePos;
            if (Vector2.Distance(mouse, P(CentroBotao.X, CentroBotao.Y)) <= RaioBotao * sc)
            {
                if (aba == Aba.Playlist)
                {
                    pedindo = true;
                    focarPedido = true;
                }
                else plugin.Chat.SetStatus("Abra a playlist (MENU) para pedir musicas.", 3);
            }
        }
    }

    public void Dispose() { }
}
