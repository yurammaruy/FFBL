using System.Net.Http;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFBL;

internal sealed record MinionItem(uint Id, string Nome, string Mdl, string Mtrl);

/// <summary>
/// O telao feito com o seu minion: o Penumbra troca o visual do minion por uma parede com a nossa imagem,
/// e este codigo deixa o minion parado no ponto do telao, com o tamanho e o giro escolhidos.
/// </summary>
internal sealed class TelaoMinion
{
    private const string Etiqueta = "FFBL_Telao";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // caminhos do modelo "parede" original (Plush Cushion)
    internal const string MdlPadrao = "chara/monster/m8058/obj/body/b0001/model/m8058b0001.mdl";
    internal const string MtrlNome = "mt_m8058b0001_b.mtrl";
    internal const string MtrlPadrao = "chara/monster/m8058/obj/body/b0001/material/v0001/" + MtrlNome;
    private const string TexBase = "chara/monster/m8058/obj/body/b0001/texture/v01_unknown_b_";

    private readonly Plugin plugin;
    private readonly PenumbraBridge pen;

    private DateTime visivelDesde = DateTime.MinValue;
    private DateTime naoMexerAte = DateTime.MinValue;
    private bool modAplicado;
    private int indiceAplicado = -1;
    private string chaveAplicada = "", chaveGerando = "", chaveDesejada = "", ultimaDesejada = "";
    private DateTime mudouEm = DateTime.MinValue;
    private int contador;
    private string arquivoPronto = "";
    private string chavePronta = "";
    private string arquivoAtual = "";
    private string arquivoPreto = "";
    private string arqMaterial = "";
    private bool vendoAtras, atrasAplicado;
    private DateTime atrasMudouEm = DateTime.MinValue;
    private DateTime proximaAplicacao = DateTime.MinValue;
    private MinionItem? alvoAplicado;

    public string Estado { get; private set; } = "";
    public bool Ativo { get; private set; }
    public bool PenumbraOk { get; private set; }
    public string[] Diario => pen.Diario;
    private DateTime proximaChecagem = DateTime.MinValue;

    private static readonly MinionItem Alvo = new(0, "Plush Cushion", MdlPadrao, MtrlPadrao);
    private uint idCompanion;           // numero do Plush Cushion na lista de minions do jogo
    private DateTime proximaInvocacao = DateTime.MinValue;
    private int tentativasInvocar;

    public TelaoMinion(Plugin plugin)
    {
        this.plugin = plugin;
        pen = new PenumbraBridge(Plugin.PluginInterface);
        if (plugin.Config.ProjFonte == 3) { plugin.Config.ProjFonte = 0; plugin.SalvarConfig(); } // opcao de clipe removida
    }

    private string PastaMod => Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? "", "telao_mod");
    private string PastaSaida
    {
        get
        {
            var p = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "telao_tex");
            Directory.CreateDirectory(p);
            return p;
        }
    }

    // ------------------------------------------------------------ invocar o Plush Cushion

    private uint AcharIdCompanion()
    {
        if (idCompanion != 0) return idCompanion;
        try
        {
            foreach (var linha in Plugin.DataManager.GetExcelSheet<Companion>())
            {
                var mc = linha.Model.ValueNullable;
                if (mc != null && mc.Value.Type == 3 && mc.Value.Model == 8058) { idCompanion = linha.RowId; break; }
            }
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "FFBL: nao achei o Plush Cushion na lista de minions"); }
        return idCompanion;
    }

    #pragma warning disable CS0618
    /// <summary>
    /// Acha o meu minion, mas so conta se ele esta na tabela de objetos do jogo agora
    /// (um minion que acabou de ser dispensado pode deixar um ponteiro velho).
    /// </summary>
    private unsafe bool AcharMinion(out nint endereco, out int indice)
    {
        endereco = 0; indice = -1;
        var eu = Plugin.ObjectTable.LocalPlayer;
        if (eu == null) return false;
        nint apontado = 0;
        var ch = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)eu.Address;
        if (ch != null) apontado = (nint)ch->CompanionObject;

        foreach (var o in Plugin.ObjectTable)
        {
            if (o.ObjectKind != ObjectKind.Companion || o.ObjectIndex <= 0) continue;
            if (o.Address == apontado || o.OwnerId == eu.EntityId)
            {
                endereco = o.Address; indice = o.ObjectIndex; return true;
            }
        }
        return false;
    }
#pragma warning restore CS0618

#pragma warning disable CS0618
    /// <summary>Acha o minion de OUTRA pessoa (o telao dela), so olhando a tabela de objetos; nao mexe em nada.</summary>
    internal string DiagEspectador { get; private set; } = "";

    private unsafe bool AcharMinionDe(string dono, out nint endereco, out int indice)
    {
        endereco = 0; indice = -1;
        DiagEspectador = "";
        if (string.IsNullOrEmpty(dono)) return false;
        uint donoId = 0;
        nint apontado = 0;
        foreach (var o in Plugin.ObjectTable)
            if (o.ObjectKind == ObjectKind.Pc && string.Equals(o.Name.TextValue, dono, StringComparison.OrdinalIgnoreCase))
            {
                donoId = o.EntityId;
                var ch = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)o.Address;
                if (ch != null) apontado = (nint)ch->CompanionObject; // o proprio personagem aponta para o minion dele
                break;
            }
        if (donoId == 0)
        {
            var nomes = new List<string>();
            foreach (var o in Plugin.ObjectTable)
                if (o.ObjectKind == ObjectKind.Pc && nomes.Count < 6) nomes.Add(o.Name.TextValue);
            DiagEspectador = $"dono '{dono}': jogador NAO achado por perto. Jogadores vistos: {string.Join(", ", nomes)}";
            return false;
        }
        var minions = 0;
        foreach (var o in Plugin.ObjectTable)
        {
            if (o.ObjectKind != ObjectKind.Companion) continue;
            minions++;
            if (o.ObjectIndex > 0 && (o.Address == apontado || o.OwnerId == donoId)) { endereco = o.Address; indice = o.ObjectIndex; return true; }
        }
        DiagEspectador = $"dono '{dono}': jogador achado, mas o minion dele NAO (minions vistos: {minions}; ponteiro do dono: {(apontado == 0 ? "vazio" : "preenchido")}).";
        return false;
    }

#pragma warning restore CS0618

    private unsafe void Invocar()
    {
        var id = AcharIdCompanion();
        if (id == 0) { Estado = "Nao achei o Plush Cushion na lista de minions do jogo."; return; }
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (am == null) return;
        var ok = am->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Companion, id);
        Plugin.Log.Information($"FFBL: usando o minion Plush Cushion (id {id}): {ok}");
        pen.Nota($"minion (id {id}) usado: {(ok ? "ok" : "o jogo recusou")}");
    }

    // ------------------------------------------------------------ ciclo

    /// <summary>Chamar a cada quadro.</summary>
    public void Tick(TelaoInfo? telao, PanelInfo? painel)
    {
        try { Passo(telao, painel); }
        catch (Exception ex)
        {
            Estado = "Erro: " + ex.Message;
            Plugin.Log.Warning(ex, "FFBL: erro no telao-minion");
        }
    }

    private bool estavaLocal, jaTinha, invocarPendente, dispensarPendente, petDeNos, modoDono;
    private DateTime naoAchouDesde = DateTime.MinValue;

    private void Passo(TelaoInfo? telao, PanelInfo? painel)
    {
        var agora = DateTime.Now;
        var local = telao != null && telao.Territorio == Plugin.ClientState.TerritoryType;

        if (!local || telao == null)
        {
            var eraAtivo = Ativo;
            if (Ativo) { Desativar(); naoMexerAte = agora.AddSeconds(1.2); }
            // o telao foi recolhido: o minion que servia de tela tambem sai
            if (jaTinha && telao == null && modoDono && (eraAtivo || petDeNos)) { dispensarPendente = true; pen.Nota("telao recolhido: dispensando o minion"); }
            jaTinha = telao != null;
            estavaLocal = false;
            invocarPendente = false;
            tentativasInvocar = 0;
            Estado = telao == null ? "Sem telao criado."
                : $"O telao esta em outro lugar (telao no mapa {telao.Territorio}, voce no mapa {Plugin.ClientState.TerritoryType}).";

            if (dispensarPendente && agora >= naoMexerAte)
            {
                dispensarPendente = false;
                petDeNos = false;
                if (AcharMinion(out _, out _)) Invocar(); // usar o minion de novo o dispensa
            }
            return;
        }

        // dono = quem criou o telao: o minion dele vira a tela. Os outros so VEEM o minion do dono; nada e invocado no jogo deles.
        var souDono = plugin.SouDono(telao);
        modoDono = souDono;
        if (!estavaLocal && souDono) { invocarPendente = true; tentativasInvocar = 0; pen.Nota("telao ativo: vou chamar o minion"); } // telao novo ou voltou ao mapa
        estavaLocal = true;
        jaTinha = true;
        dispensarPendente = false;

        if (agora >= proximaChecagem) { PenumbraOk = pen.Disponivel(); proximaChecagem = agora.AddSeconds(3); }
        if (!PenumbraOk) { Estado = "O Penumbra nao respondeu. Ele esta instalado e ligado?"; return; }
        if (!souDono) Estado = "Procurando o minion de " + telao.Dono + "...";

        if (Plugin.ObjectTable.LocalPlayer == null) return;

        // acha o meu minion (o proprio personagem aponta para ele)
        nint enderecoMinion; int indice;
        var achou = souDono ? AcharMinion(out enderecoMinion, out indice) : AcharMinionDe(telao.Dono, out enderecoMinion, out indice);
        var alvo = Alvo;
        if (!achou && !souDono)
        {
            // espectador: o minion de quem criou o telao nao esta por perto agora; nao invoca nada
            if (Ativo) { Desativar(); naoMexerAte = agora.AddSeconds(1.2); }
            visivelDesde = DateTime.MinValue;
            Estado = "Aguardando o minion de " + telao.Dono + " ficar visivel perto de voce. " + DiagEspectador;
            return;
        }
        if (!achou)
        {
            visivelDesde = DateTime.MinValue;
            if (naoAchouDesde == DateTime.MinValue) naoAchouDesde = agora;
            Estado = invocarPendente ? "Invocando o Plush Cushion..." : "Aguardando o minion.";
            // so invoca uma vez por telao, depois de esperar um pouco (o minion pode estar so sendo redesenhado)
            if (invocarPendente && (agora - naoAchouDesde).TotalSeconds > 1.0 && agora >= naoMexerAte
                && agora >= proximaInvocacao && tentativasInvocar < 3)
            {
                tentativasInvocar++;
                proximaInvocacao = agora.AddSeconds(4);
                petDeNos = true;
                Invocar();
            }
            else if (tentativasInvocar >= 3 && invocarPendente) Estado = "Nao consegui invocar o Plush Cushion. Invoque pelo menu de minions.";
            return;
        }
        naoAchouDesde = DateTime.MinValue;
        invocarPendente = false;
        tentativasInvocar = 0;
        if (visivelDesde == DateTime.MinValue) visivelDesde = agora;
        var aparecido = (agora - visivelDesde).TotalSeconds > 1.5;

        // qual imagem queremos agora (so muda se a fonte mudar; o tamanho nao exige nova imagem)
        chaveDesejada = ChaveImagem(telao, painel);
        if (chaveDesejada != ultimaDesejada) { ultimaDesejada = chaveDesejada; mudouEm = agora; }
        if (chaveDesejada != chaveAplicada && chaveDesejada != chaveGerando && (agora - mudouEm).TotalSeconds > 0.8)
        {
            chaveGerando = chaveDesejada;
            var chave = chaveDesejada;
            var fonte = ObterFonte(painel);
            var aj = AjusteAtual();
            var teste = plugin.Config.ProjTeste;
            var arquivo = Path.Combine(PastaSaida, $"d_{++contador}.tex");
            _ = Task.Run(async () =>
            {
                try
                {
                    var bytes = teste ? Array.Empty<byte>() : await fonte();
                    await GerarTexturaAsync(teste ? null : bytes, aj, arquivo);
                    arquivoPronto = arquivo;
                    chavePronta = chave;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warning(ex, "FFBL: nao consegui montar a textura");
                    Estado = "Nao consegui montar a imagem: " + ex.Message;
                    chaveGerando = "";
                }
            });
        }

        var livre = aparecido && agora >= naoMexerAte;
        if (livre)
        {
            var precisa = false;
            if (!string.IsNullOrEmpty(arquivoPronto))
            {
                arquivoAtual = arquivoPronto; chaveAplicada = chavePronta; arquivoPronto = ""; chaveGerando = "";
                precisa = true;
            }
            if (indice != indiceAplicado || alvo != alvoAplicado) precisa = true;

            // opcional: do lado de tras o telao fica preto (redesenha o minion ao cruzar, pode piscar)
            if (plugin.Config.ProjCostasPretas)
            {
                if (VerLadoDeTras(telao, out var atras) && atras != vendoAtras) { vendoAtras = atras; atrasMudouEm = agora; }
                if (vendoAtras != atrasAplicado && (agora - atrasMudouEm).TotalSeconds > 1.5) precisa = true;
            }
            else if (vendoAtras)
            {
                vendoAtras = false;
                if (atrasAplicado) precisa = true;
            }

            if (precisa && !string.IsNullOrEmpty(arquivoAtual) && agora >= proximaAplicacao)
            {
                Aplicar(alvo, indice);
                if (!Ativo || indice != indiceAplicado) proximaAplicacao = agora.AddSeconds(6); // falhou: espera antes de tentar de novo
            }

            // deixa o minion parado, do tamanho e virado como o telao
            Fixar(enderecoMinion, telao);
        }

        var quem = souDono ? "seu minion" : "o minion de " + telao.Dono;
        Estado = !aparecido ? $"Achei {quem} (objeto {indice}). Preparando..."
            : Ativo ? $"Telao aplicado em {quem} (objeto {indice})."
            : "Preparando a imagem...";
        if (!souDono && aparecido && string.IsNullOrEmpty(arquivoAtual)) Estado += " Esperando a imagem ficar pronta.";
    }

    internal static readonly string[] ExtImagem = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };

    /// <summary>True se o arquivo e uma imagem parada que o telao sabe mostrar.</summary>
    internal static bool ImagemValida(string caminho)
        => !string.IsNullOrEmpty(caminho) && ExtImagem.Contains(Path.GetExtension(caminho).ToLowerInvariant()) && File.Exists(caminho);

    private string ChaveImagem(TelaoInfo t, PanelInfo? p)
    {
        var c = plugin.Config;
        var origem = c.ProjFonte switch
        {
            1 or 3 when p != null && p.Estado != "Parado" && !string.IsNullOrEmpty(p.Thumb) => "thumb|" + p.Thumb,
            2 when ImagemValida(c.ProjImagemLocal) => "local|" + c.ProjImagemLocal + "|" + File.GetLastWriteTimeUtc(c.ProjImagemLocal).Ticks,
            _ => "padrao",
        };
        return $"{(c.ProjTeste ? "teste" : origem)}|{EspelharEfetivo()}|{c.ProjEspelharV}|{c.ProjImgDx:F3}|{c.ProjImgDy:F3}|{c.ProjImgEx:F3}|{c.ProjImgEy:F3}|{c.ProjBrilho:F2}";
    }

    private Func<Task<byte[]>> ObterFonte(PanelInfo? p)
    {
        var c = plugin.Config;
        var padrao = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? "", "images", "telao.png");
        if ((c.ProjFonte == 1 || c.ProjFonte == 3) && p != null && p.Estado != "Parado" && !string.IsNullOrEmpty(p.Thumb))
        {
            var url = p.Thumb;
            return async () => await Http.GetByteArrayAsync(url);
        }
        if (c.ProjFonte == 2 && ImagemValida(c.ProjImagemLocal))
        {
            var cam = c.ProjImagemLocal;
            return () => File.ReadAllBytesAsync(cam);
        }
        return () => File.ReadAllBytesAsync(padrao);
    }

    // ------------------------------------------------------------ Penumbra

    private void Aplicar(MinionItem alvo, int indice)
    {
        if (modAplicado) pen.RemoverModTodos(Etiqueta);

        var mod = PastaMod;
        arqMaterial = MaterialComBrilho(Path.Combine(mod, "material.mtrl"));
        var arqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [alvo.Mdl] = Path.Combine(mod, "modelo.mdl"),
            [alvo.Mtrl] = arqMaterial,
            [TexBase + "d.tex"] = vendoAtras ? TexturaPreta() : arquivoAtual,
            [TexBase + "n.tex"] = Path.Combine(mod, "n.tex"),
            [TexBase + "id.tex"] = Path.Combine(mod, "id.tex"),
            [TexBase + "s.tex"] = Path.Combine(mod, "s.tex"),
        };
        modAplicado = pen.AdicionarModTodos(Etiqueta, arqs);
        if (!modAplicado) { Estado = "O Penumbra recusou a troca (veja os detalhes tecnicos)."; return; }

        pen.Redesenhar(indice);
        naoMexerAte = DateTime.Now.AddSeconds(1.8); // nao toca no minion enquanto ele e redesenhado

        indiceAplicado = indice;
        alvoAplicado = alvo;
        atrasAplicado = vendoAtras;
        Ativo = true;
        LimparTexturasAntigas();
    }

    /// <summary>
    /// Se o brilho extra estiver ligado, faz uma copia do material com "luz propria" (emissao) em todas as linhas,
    /// para a tela ficar iluminada dos dois lados. Se o arquivo nao tiver o formato esperado, usa o original.
    /// </summary>
    private string MaterialComBrilho(string original)
    {
        var br = plugin.Config.ProjBrilho;
        if (br < 0.001f) return original;
        try
        {
            var b = File.ReadAllBytes(original);
            int dsz = BitConverter.ToUInt16(b, 4), ssz = BitConverter.ToUInt16(b, 6);
            int tc = b[12], uvc = b[13], csc = b[14], add = b[15];
            var ct = 16 + (tc + uvc + csc) * 4 + ssz + add;
            if (dsz != 2176 || ct + 2048 > b.Length) return original;
            var h = BitConverter.GetBytes((Half)Math.Clamp(br, 0f, 4f));
            for (var linha = 0; linha < 64; linha += 2) // linhas de cor (as de numero impar guardam outros dados)
                for (var k = 8; k <= 10; k++)
                {
                    var p = ct + linha * 32 + k * 2;
                    b[p] = h[0]; b[p + 1] = h[1];
                }
            var destino = Path.Combine(PastaSaida, $"mat_{++contador}.mtrl");
            File.WriteAllBytes(destino, b);
            return destino;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "FFBL: nao consegui montar o material com brilho");
            return original;
        }
    }

    private string TexturaPreta()
    {
        if (!string.IsNullOrEmpty(arquivoPreto) && File.Exists(arquivoPreto)) return arquivoPreto;
        arquivoPreto = Path.Combine(PastaSaida, "preto.tex");
        var px = new byte[8 * 8 * 4];
        for (var i = 3; i < px.Length; i += 4) px[i] = 255;
        EscreverTex(arquivoPreto, 8, px);
        return arquivoPreto;
    }

    /// <summary>True se a camera esta do lado de tras do telao (pelo sentido dos cantos na tela).</summary>
    private bool VerLadoDeTras(TelaoInfo t, out bool atras)
    {
        atras = false;
        var r = (t.Giro + plugin.Config.ProjGiroModelo) * MathF.PI / 180f;
        var dir = new Vector3(MathF.Cos(r), 0f, -MathF.Sin(r));
        var meio = dir * (t.Largura / 2f);
        var alto = Vector3.UnitY * t.Altura;
        if (!Plugin.GameGui.WorldToScreen(t.Pos - meio + alto, out var tl, out var a)) return false;
        if (!Plugin.GameGui.WorldToScreen(t.Pos + meio + alto, out var tr, out var b)) return false;
        if (!Plugin.GameGui.WorldToScreen(t.Pos + meio, out var br, out var c)) return false;
        if (!Plugin.GameGui.WorldToScreen(t.Pos - meio, out var bl, out var d)) return false;
        if (!(a && b && c && d)) return false;
        var area = (tl.X * tr.Y - tr.X * tl.Y) + (tr.X * br.Y - br.X * tr.Y)
                 + (br.X * bl.Y - bl.X * br.Y) + (bl.X * tl.Y - tl.X * bl.Y);
        atras = area < 0;
        return true;
    }

    private void LimparTexturasAntigas()
    {
        try
        {
            foreach (var f in Directory.GetFiles(PastaSaida, "mat_*.mtrl"))
                if (!string.Equals(f, arqMaterial, StringComparison.OrdinalIgnoreCase)) File.Delete(f);
            foreach (var f in Directory.GetFiles(PastaSaida, "d_*.tex"))
                if (!string.Equals(f, arquivoAtual, StringComparison.OrdinalIgnoreCase) && !f.EndsWith("preto.tex")) File.Delete(f);
        }
        catch { /* arquivo em uso: fica para a proxima */ }
    }

    public void Reaplicar()
    {
        chaveAplicada = "";
        ultimaDesejada = "";
    }

    public void Desativar()
    {
        try
        {
            if (modAplicado) pen.RemoverModTodos(Etiqueta);
            if (indiceAplicado > 0) pen.Redesenhar(indiceAplicado);
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "FFBL: erro ao desfazer o telao-minion"); }
        modAplicado = false;
        indiceAplicado = -1;
        alvoAplicado = null;
        chaveAplicada = "";
        chaveGerando = "";
        Ativo = false;
    }

    // ------------------------------------------------------------ posicao e tamanho (codigo "perigoso": mexe no minion)

    private unsafe void Fixar(nint endereco, TelaoInfo t)
    {
        var g = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)endereco;
        if (g == null) return;
        var d = g->DrawObject;
        if (d == null) return; // o minion ainda nao tem corpo desenhado: mexer agora derruba o jogo

        var c = plugin.Config;
        var giro = (t.Giro + c.ProjGiroModelo) * MathF.PI / 180f;
        g->SetPosition(t.Pos.X, t.Pos.Y, t.Pos.Z);
        g->SetRotation(giro);

        if (MathF.Abs(c.ProjRotX) > 0.01f || MathF.Abs(c.ProjRotZ) > 0.01f)
        {
            d->Object.Rotation = Quaternion.CreateFromYawPitchRoll(
                giro, c.ProjRotX * MathF.PI / 180f, c.ProjRotZ * MathF.PI / 180f);
        }

        d->Object.Scale = new Vector3(
            t.Largura / MathF.Max(c.ProjBaseLargura, 0.1f),
            t.Altura / MathF.Max(c.ProjBaseAltura, 0.1f),
            MathF.Max(c.ProjEspessura, 0.01f));
    }

    // ------------------------------------------------------------ textura (.tex) a partir de uma imagem

    private readonly record struct Ajuste(bool EspH, bool EspV, float Dx, float Dy, float Ex, float Ey);

    /// <summary>
    /// A face clara do modelo mostra a imagem espelhada e a face escura mostra certa.
    /// Por isso, no lado "normal" (face clara) a imagem e virada sozinha; no "invertido" nao.
    /// A caixa "Virar imagem na horizontal" desfaz isso, se precisar.
    /// </summary>
    private bool EspelharEfetivo()
    {
        var c = plugin.Config;
        var invertido = c.ProjGiroModelo >= 90f && c.ProjGiroModelo < 270f;
        return c.ProjEspelharH ^ !invertido;
    }

    private Ajuste AjusteAtual()
    {
        var c = plugin.Config;
        return new Ajuste(EspelharEfetivo(), c.ProjEspelharV, c.ProjImgDx, c.ProjImgDy,
            Math.Max(c.ProjImgEx, 0.05f), Math.Max(c.ProjImgEy, 0.05f));
    }

    /// <summary>
    /// Monta a textura 1024x1024 do telao. Moldura padrao = proporcao do poster FFBL (1448x1086):
    /// a imagem aparece inteira dentro dela e o que sobra fica preto. Com imagem == null desenha a grade de teste.
    /// </summary>
    private static async Task GerarTexturaAsync(byte[]? imagem, Ajuste aj, string destino)
    {
        const int N = 1024;
        const float Moldura = 1448f / 1086f;

        byte[]? raw = null;
        int w = 1, h = 1, pitch = 0;
        bool rgba = false;
        if (imagem != null)
        {
            using var wrap = await Plugin.TextureProvider.CreateFromImageAsync(imagem);
            var (spec, dados) = await Plugin.Leitura.GetRawImageAsync(wrap);
            var fmt = spec.DxgiFormat;
            bool bgra = fmt is 87 or 91;
            rgba = fmt is 28 or 29;
            if (!bgra && !rgba) throw new InvalidOperationException($"formato de imagem {fmt} nao suportado");
            raw = dados; w = spec.Width; h = spec.Height; pitch = spec.Pitch;
        }
        float imgAsp = (float)w / h;
        var saida = new byte[N * N * 4];

        Parallel.For(0, N, y =>
        {
            float ty = (y + 0.5f) / N;
            for (int x = 0; x < N; x++)
            {
                float tx = (x + 0.5f) / N;
                // posicao na "moldura" (0..1), levando em conta o ajuste de deslocamento/escala/espelho
                float fx = (tx - 0.5f - aj.Dx) / aj.Ex + 0.5f;
                float fy = (ty - 0.5f - aj.Dy) / aj.Ey + 0.5f;
                if (aj.EspH) fx = 1 - fx;
                if (aj.EspV) fy = 1 - fy;
                int o = (y * N + x) * 4;
                saida[o + 3] = 255;
                if (fx < 0f || fx > 1f || fy < 0f || fy > 1f) continue; // fora da moldura: preto

                if (raw == null)
                {
                    // grade de teste: R = esquerda->direita, G = cima->baixo, linhas a cada 1/8, triangulo no canto de cima/esquerda
                    byte r = (byte)(fx * 255), g = (byte)(fy * 255), bl = 70;
                    bool linha = MathF.Abs(fx * 8 - MathF.Round(fx * 8)) < 0.012f || MathF.Abs(fy * 8 - MathF.Round(fy * 8)) < 0.012f;
                    bool meio = MathF.Abs(fx - 0.5f) < 0.006f || MathF.Abs(fy - 0.5f) < 0.006f;
                    bool borda = fx < 0.012f || fx > 0.988f || fy < 0.012f || fy > 0.988f;
                    bool canto = fx + fy < 0.18f;
                    if (canto) { r = g = bl = 255; }
                    else if (meio) { r = g = bl = 0; }
                    else if (borda) { r = 255; g = 0; bl = 255; }
                    else if (linha) { r = g = bl = 255; }
                    saida[o] = bl; saida[o + 1] = g; saida[o + 2] = r;
                    continue;
                }

                // imagem inteira dentro da moldura
                float uu = imgAsp >= Moldura ? fx : 0.5f + (fx - 0.5f) * (Moldura / imgAsp);
                float vv = imgAsp >= Moldura ? 0.5f + (fy - 0.5f) * (imgAsp / Moldura) : fy;
                if (uu < 0f || uu > 1f || vv < 0f || vv > 1f) continue; // faixa preta

                float sy = vv * h - 0.5f;
                int y0 = Math.Clamp((int)MathF.Floor(sy), 0, h - 1);
                int y1 = Math.Min(y0 + 1, h - 1);
                float wy = Math.Clamp(sy - y0, 0f, 1f);
                float sx = uu * w - 0.5f;
                int x0 = Math.Clamp((int)MathF.Floor(sx), 0, w - 1);
                int x1 = Math.Min(x0 + 1, w - 1);
                float wx = Math.Clamp(sx - x0, 0f, 1f);
                for (int ch = 0; ch < 3; ch++)
                {
                    float a = raw[y0 * pitch + x0 * 4 + ch], b = raw[y0 * pitch + x1 * 4 + ch];
                    float c2 = raw[y1 * pitch + x0 * 4 + ch], d = raw[y1 * pitch + x1 * 4 + ch];
                    float val = (a * (1 - wx) + b * wx) * (1 - wy) + (c2 * (1 - wx) + d * wx) * wy;
                    int dst = rgba ? (ch == 0 ? 2 : ch == 2 ? 0 : ch) : ch; // saida em B, G, R
                    saida[o + dst] = (byte)Math.Clamp(val + 0.5f, 0f, 255f);
                }
            }
        });

        EscreverTex(destino, N, saida);
    }

    private static void EscreverTex(string destino, int n, byte[] bgra)
    {
        // cabecalho .tex: formato B8G8R8A8 sem compressao, 1 nivel
        var cab = new byte[80];
        BitConverter.GetBytes(0x00800000u).CopyTo(cab, 0);
        BitConverter.GetBytes(0x1450u).CopyTo(cab, 4);
        BitConverter.GetBytes((ushort)n).CopyTo(cab, 8);
        BitConverter.GetBytes((ushort)n).CopyTo(cab, 10);
        BitConverter.GetBytes((ushort)1).CopyTo(cab, 12);
        BitConverter.GetBytes((ushort)1).CopyTo(cab, 14);
        BitConverter.GetBytes(0u).CopyTo(cab, 16);
        BitConverter.GetBytes(1u).CopyTo(cab, 20);
        BitConverter.GetBytes(2u).CopyTo(cab, 24);
        BitConverter.GetBytes(80u).CopyTo(cab, 28);

        using var fs = File.Create(destino);
        fs.Write(cab);
        fs.Write(bgra);
    }

    public void Dispose()
    {
        Desativar();
    }
}
