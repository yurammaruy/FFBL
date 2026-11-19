using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace FFBL;

/// <summary>
/// OMNE (voz por proximidade) dentro do FFBL. Codigo trazido do plugin OMNE v0.4.
/// So funciona quando "Ativar o OMNE" esta marcado (padrao: desligado).
/// </summary>
internal sealed class OmneModulo : IDisposable
{
    private readonly Plugin plugin;
    private readonly IFramework framework;
    private readonly ITargetManager alvo;

    private static readonly IReadOnlyDictionary<string, int> SemVolumes = new Dictionary<string, int>();
    private static readonly IReadOnlyDictionary<string, float> SemDistancias = new Dictionary<string, float>();
    private DateTime proximo = DateTime.MinValue;
    private volatile IReadOnlyDictionary<string, float> distancias = SemDistancias;

    private string msgVinculo = "", textoLista = "";
    private bool listaSuja = true;

    public OmneRpc Rpc { get; }
    private OmneConfig Cfg => plugin.Config.Omne;

    public OmneModulo(Plugin plugin, IFramework framework, ITargetManager alvo)
    {
        this.plugin = plugin;
        this.framework = framework;
        this.alvo = alvo;
        Rpc = new OmneRpc(plugin.Config.Omne, plugin.SalvarConfig, Plugin.Log);
    }

    // ------------------------------------------------------------ distancia -> volume (a cada ~200 ms)

    public void Tick()
    {
        if (!plugin.Config.OmneAtivo)
        {
            if (Rpc.Running) Rpc.Stop(); // desligou: devolve o volume de todos para 100%
            return;
        }

        var agora = DateTime.UtcNow;
        if (agora < proximo) return;
        proximo = agora.AddMilliseconds(200);

        try
        {
            if (!Cfg.ProximityEnabled || Cfg.Links.Count == 0 || !Rpc.Connected)
            {
                Rpc.SetDesired(SemVolumes);
                distancias = SemDistancias;
                return;
            }

            var eu = Plugin.ObjectTable.LocalPlayer;
            if (eu == null) return;

            var perto = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is not IPlayerCharacter pc) continue;
                if (pc.GameObjectId == eu.GameObjectId) continue;
                perto[pc.Name.TextValue] = pc.Position;
            }

            var cheio = Math.Max(0.5f, Cfg.FullDistance);
            var max = Math.Max(cheio + 0.5f, Cfg.MaxDistance);
            var maxPct = Math.Clamp(Cfg.MaxVolumePercent, 0, 200);

            var querido = new Dictionary<string, int>();
            var dist = new Dictionary<string, float>();
            foreach (var (discordId, nome) in Cfg.Links)
            {
                if (string.IsNullOrWhiteSpace(nome)) continue;
                var volume = 0;
                if (perto.TryGetValue(nome.Trim(), out var pos))
                {
                    var d = Vector3.Distance(eu.Position, pos);
                    dist[discordId] = d;
                    var f = d <= cheio ? 1f : d >= max ? 0f : 1f - (d - cheio) / (max - cheio);
                    volume = (int)MathF.Round(f * maxPct);
                }
                querido[discordId] = volume;
            }
            Rpc.SetDesired(querido);
            distancias = dist;
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "OMNE: falha ao calcular a distancia"); }
    }

    private string? MeuNome() { try { return Plugin.ObjectTable.LocalPlayer?.Name.TextValue; } catch { return null; } }
    private string? NomeDoAlvo() { try { return alvo.Target is IPlayerCharacter pc ? pc.Name.TextValue : null; } catch { return null; } }

    // ------------------------------------------------------------ tela (aba OMNE)

    private static string SoDigitos(string t) => new(t.Where(char.IsDigit).ToArray());

    private static string ExtrairId(string t)
    {
        var partes = t.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries);
        return SoDigitos(partes.Length > 0 ? partes[^1] : "");
    }

    private static readonly Vector4 Aviso = new(1f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 Laranja = new(1f, 0.55f, 0.4f, 1f);
    private static readonly Vector4 Cinza = new(0.7f, 0.7f, 0.7f, 1f);

    public void Desenhar(float largura)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.Text(Loc.T("OMNE - voz por proximidade"));
        var ativo = plugin.Config.OmneAtivo;
        if (ImGui.Checkbox(Loc.T("Ativar o OMNE"), ref ativo))
        {
            plugin.Config.OmneAtivo = ativo;
            plugin.SalvarConfig();
        }

        if (!plugin.Config.OmneAtivo)
        {
            ImGui.TextColored(Cinza, Loc.T("Desativado."));
            ImGui.PopTextWrapPos();
            return;
        }

        ImGui.Separator();
        Conexao(largura);
        ImGui.Separator();
        Proximidade(largura);
        ImGui.Separator();
        Membros(largura);
        ImGui.Spacing();
        Lista();
        ImGui.PopTextWrapPos();
    }

    private void Conexao(float largura)
    {
        var rpc = Rpc;
        if (string.IsNullOrEmpty(Cfg.ClientId) || string.IsNullOrEmpty(Cfg.ChannelId))
            ImGui.TextColored(Aviso, Loc.T("Cole o codigo de conexao na aba FFBL para preencher os dados do OMNE."));

        if (!rpc.Running)
        {
            if (ImGui.Button(Loc.T("Conectar##omne"))) { plugin.SalvarConfig(); rpc.Start(); }
        }
        else if (ImGui.Button(Loc.T("Desconectar##omne"))) rpc.Stop();

        if (rpc.Connected)
        {
            ImGui.SameLine();
            if (ImGui.Button(Loc.T("Entrar na sala"))) { plugin.SalvarConfig(); rpc.RequestJoin(); }
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Esquecer autorizacao"))) { Cfg.AccessToken = ""; plugin.SalvarConfig(); }

        ImGui.TextColored(rpc.InRoom ? new Vector4(0.4f, 0.9f, 0.5f, 1f) : new Vector4(0.9f, 0.8f, 0.4f, 1f), rpc.Status);
        if (rpc.Notice.Length > 0) ImGui.TextColored(Laranja, rpc.Notice);
    }

    private void Proximidade(float largura)
    {
        if (!ImGui.TreeNodeEx(Loc.T("Proximidade (volume pela distancia)"), ImGuiTreeNodeFlags.DefaultOpen)) return;

        var on = Cfg.ProximityEnabled;
        if (ImGui.Checkbox(Loc.T("Ativar a proximidade"), ref on)) { Cfg.ProximityEnabled = on; plugin.SalvarConfig(); }

        ImGui.SetNextItemWidth(largura * 0.6f);
        var cheio = Cfg.FullDistance;
        if (ImGui.SliderFloat("Volume total ate (yalms)", ref cheio, 1f, 40f, "%.0f"))
        { Cfg.FullDistance = Math.Min(cheio, Cfg.MaxDistance - 1f); plugin.SalvarConfig(); }

        ImGui.SetNextItemWidth(largura * 0.6f);
        var max = Cfg.MaxDistance;
        if (ImGui.SliderFloat("Silencio a partir de (yalms)", ref max, 2f, 100f, "%.0f"))
        { Cfg.MaxDistance = Math.Max(max, Cfg.FullDistance + 1f); plugin.SalvarConfig(); }

        ImGui.SetNextItemWidth(largura * 0.6f);
        var pct = Cfg.MaxVolumePercent;
        if (ImGui.SliderInt("Volume maximo (%)", ref pct, 10, 200)) { Cfg.MaxVolumePercent = pct; plugin.SalvarConfig(); }
        ImGui.TextColored(Cinza, Loc.T("100% = volume normal do Discord."));

        if (ImGui.Button(Loc.T("Restaurar o volume de todos (100%)"))) Rpc.RequestResetAll();
        if (Rpc.VolumeNotice.Length > 0) ImGui.TextColored(Laranja, Rpc.VolumeNotice);
        ImGui.TreePop();
    }

    private void Membros(float largura)
    {
        ImGui.Text(Loc.T("Quem e quem na sala do OMNE"));
        ImGui.Text(Loc.T("Escreva o nome do personagem de cada pessoa (como aparece no jogo). Dica: botao Alvo."));

        var membros = Rpc.Members;
        if (membros.Count == 0) { ImGui.TextColored(Cinza, Loc.T("(ninguem ainda)")); return; }

        var meuId = Rpc.MyId;
        foreach (var m in membros)
        {
            ImGui.PushID(m.Id);
            var sou = m.Id == meuId;
            ImGui.Text(sou ? m.Name + " (voce)" : m.Name);

            Cfg.Links.TryGetValue(m.Id, out var atual);
            var nome = atual ?? "";
            ImGui.SetNextItemWidth(Math.Min(200, largura * 0.55f));
            if (ImGui.InputText("##char", ref nome, 64)) Vincular(m.Id, nome);

            ImGui.SameLine();
            if (sou)
            {
                if (ImGui.Button(Loc.T("Eu")))
                {
                    var eu = MeuNome();
                    if (eu != null) { Vincular(m.Id, eu); msgVinculo = ""; }
                    else msgVinculo = "Nao consegui ler o seu personagem agora.";
                }
            }
            else if (ImGui.Button(Loc.T("Alvo")))
            {
                var a = NomeDoAlvo();
                if (a != null) { Vincular(m.Id, a); msgVinculo = ""; }
                else msgVinculo = "Clique no personagem da pessoa (um jogador) e tente de novo.";
            }

            if (!sou && !string.IsNullOrWhiteSpace(nome))
            {
                var aplicado = Rpc.GetApplied(m.Id);
                var t = distancias.TryGetValue(m.Id, out var d) ? $"distancia {d:0.0} yalms" : "fora de alcance / outro mapa";
                if (aplicado.HasValue) t += $"  ->  volume {aplicado.Value}%";
                ImGui.TextColored(Cinza, "    " + t);
            }
            else if (!sou) ImGui.TextColored(Cinza, Loc.T("    sem personagem: o volume desta pessoa nao e alterado"));

            ImGui.PopID();
            ImGui.Spacing();
        }
        if (msgVinculo.Length > 0) ImGui.TextColored(Laranja, msgVinculo);
    }

    private void Vincular(string discordId, string nome)
    {
        var limpo = nome.Trim();
        if (limpo.Length == 0) Cfg.Links.Remove(discordId); else Cfg.Links[discordId] = limpo;
        plugin.SalvarConfig();
        listaSuja = true;
    }

    private void Lista()
    {
        if (!ImGui.TreeNodeEx(Loc.T("Compartilhar a lista de personagens"))) return;
        ImGui.Text(Loc.T("Cada linha: Nome do Personagem = ID do Discord. Copie (Ctrl+A, Ctrl+C) e envie ao grupo; quem recebe cola no lugar."));

        if (listaSuja)
        {
            var sb = new StringBuilder();
            foreach (var (id, nome) in Cfg.Links.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
                sb.Append(nome).Append(" = ").Append(id).Append('\n');
            textoLista = sb.ToString();
            listaSuja = false;
        }
        if (ImGui.InputTextMultiline("##omnelista", ref textoLista, 4000, new Vector2(-1, 120)))
        {
            var novos = new Dictionary<string, string>();
            foreach (var cru in textoLista.Split('\n'))
            {
                var linha = cru.Trim();
                var i = linha.LastIndexOf('=');
                if (i <= 0) continue;
                var nome = linha[..i].Trim();
                var id = SoDigitos(linha[(i + 1)..]);
                if (nome.Length > 0 && id.Length > 0) novos[id] = nome;
            }
            Cfg.Links.Clear();
            foreach (var kv in novos) Cfg.Links[kv.Key] = kv.Value;
            plugin.SalvarConfig();
        }
        ImGui.TreePop();
    }

    public void Dispose()
    {
        try { Rpc.Dispose(); } catch { /* ignora */ }
    }
}
