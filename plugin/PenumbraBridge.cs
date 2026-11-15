using Dalamud.Plugin;

namespace FFBL;

/// <summary>
/// Conversa com o Penumbra (o plugin que troca modelos e texturas do jogo).
/// Cada chamada tenta mais de um nome possivel, porque o Penumbra ja mudou esses nomes antes.
/// Tudo o que acontece fica em Diario, para podermos ver na tela se algo falhou.
/// </summary>
internal sealed class PenumbraBridge
{
    private readonly IDalamudPluginInterface pi;
    private readonly List<string> diario = new();
    private readonly object gate = new();

    public PenumbraBridge(IDalamudPluginInterface pi) { this.pi = pi; }

    public string[] Diario { get { lock (gate) return diario.ToArray(); } }

    public void Nota(string s) => Anotar(s);

    private void Anotar(string s)
    {
        lock (gate)
        {
            if (diario.Count > 0 && diario[^1].EndsWith(s)) return; // nao repete a mesma linha
            diario.Add($"{DateTime.Now:HH:mm:ss} {s}");
            while (diario.Count > 8) diario.RemoveAt(0);
        }
        Plugin.Log.Information("FFBL/Penumbra: " + s);
    }

    private static readonly string[] Sufixos = { "", ".V5", ".V6" };

    /// <summary>Tenta chamar com cada variacao do nome. Devolve false se nenhuma funcionou.</summary>
    private bool Tentar<T>(string nome, Func<string, T> chamada, out T? resultado)
    {
        var erros = new List<string>();
        foreach (var suf in Sufixos)
        {
            try
            {
                resultado = chamada(nome + suf);
                return true;
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Length > 70 ? ex.Message[..70] : ex.Message;
                erros.Add($"[{(suf == "" ? "sem sufixo" : suf)}] {ex.GetType().Name}: {msg}");
            }
        }
        Anotar($"{nome} falhou: " + string.Join(" | ", erros));
        resultado = default;
        return false;
    }

    /// <summary>True se o Penumbra respondeu.</summary>
    private bool versaoAnotada;

    public bool Disponivel()
    {
        if (!versaoAnotada)
        {
            versaoAnotada = true;
            foreach (var suf in new[] { "", ".V5", ".V6" })
            {
                try
                {
                    var v = pi.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersion" + suf).InvokeFunc();
                    Anotar($"Penumbra.ApiVersion{suf} = {v.Item1}.{v.Item2}");
                    break;
                }
                catch { /* tenta o proximo */ }
            }
        }
        try
        {
            foreach (var suf in new[] { "", ".V5", ".V6" })
            {
                try
                {
                    pi.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersion" + suf).InvokeFunc();
                    return true;
                }
                catch { /* tenta o proximo */ }
            }
            // versao antiga: ApiVersions
            pi.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersions").InvokeFunc();
            return true;
        }
        catch { return false; }
    }

    public Guid CriarColecao(string nome)
    {
        // Penumbra 1.7: CreateTemporaryCollection.V6(identidade, nome) -> (codigo, id)
        if (Tentar("Penumbra.CreateTemporaryCollection", l => pi.GetIpcSubscriber<string, string, (int, Guid)>(l).InvokeFunc(nome, nome), out (int, Guid) r))
        {
            Anotar($"CriarColecao codigo {r.Item1}");
            return r.Item1 == 0 ? r.Item2 : Guid.Empty;
        }
        return Guid.Empty;
    }

    public bool AtribuirColecao(Guid colecao, int indiceObjeto)
    {
        if (!Tentar("Penumbra.AssignTemporaryCollection", l => pi.GetIpcSubscriber<Guid, int, bool, int>(l).InvokeFunc(colecao, indiceObjeto, true), out int ec))
            return false;
        Anotar($"AtribuirColecao codigo {ec}");
        return ec == 0;
    }

    public bool AdicionarMod(string etiqueta, Guid colecao, Dictionary<string, string> arquivos)
    {
        if (!Tentar("Penumbra.AddTemporaryMod", l => pi.GetIpcSubscriber<string, Guid, Dictionary<string, string>, string, int, int>(l)
                .InvokeFunc(etiqueta, colecao, arquivos, "", 0), out int ec))
            return false;
        Anotar($"AdicionarMod codigo {ec} ({arquivos.Count} arquivos)");
        return ec == 0;
    }

    public void RemoverMod(string etiqueta, Guid colecao)
    {
        if (Tentar("Penumbra.RemoveTemporaryMod", l => pi.GetIpcSubscriber<string, Guid, int, int>(l).InvokeFunc(etiqueta, colecao, 0), out int ec))
            Anotar($"RemoverMod codigo {ec}");
    }

    public void ApagarColecao(Guid colecao)
    {
        if (Tentar("Penumbra.DeleteTemporaryCollection", l => pi.GetIpcSubscriber<Guid, int>(l).InvokeFunc(colecao), out int ec))
            Anotar($"ApagarColecao codigo {ec}");
    }

    /// <summary>Troca temporaria valendo para todas as colecoes (so no seu jogo).</summary>
    public bool AdicionarModTodos(string etiqueta, Dictionary<string, string> arquivos)
    {
        if (!Tentar("Penumbra.AddTemporaryModAll", l => pi.GetIpcSubscriber<string, Dictionary<string, string>, string, int, int>(l)
                .InvokeFunc(etiqueta, arquivos, "", 0), out int ec))
            return false;
        Anotar($"AdicionarModTodos codigo {ec} ({arquivos.Count} arquivos)");
        return ec == 0;
    }

    public void RemoverModTodos(string etiqueta)
    {
        if (Tentar("Penumbra.RemoveTemporaryModAll", l => pi.GetIpcSubscriber<string, int, int>(l).InvokeFunc(etiqueta, 0), out int ec))
            Anotar($"RemoverModTodos codigo {ec}");
    }

    public void Redesenhar(int indiceObjeto)
    {
        foreach (var nome in new[] { "Penumbra.RedrawObject", "Penumbra.RedrawObjectByIndex" })
        {
            if (Tentar(nome, l => { pi.GetIpcSubscriber<int, int, object>(l).InvokeAction(indiceObjeto, 0); return true; }, out bool _))
            {
                Anotar($"Redesenhar objeto {indiceObjeto}");
                return;
            }
        }
    }
}
