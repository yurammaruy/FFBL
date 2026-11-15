using System.Net.Http;
using Dalamud.Interface.Textures.TextureWraps;

namespace FFBL;

/// <summary>Baixa a capa da musica (miniatura do YouTube) em segundo plano e entrega como textura.</summary>
internal sealed class ThumbCache : IDisposable
{
    private static readonly HttpClient Http = CriarCliente();

    private static HttpClient CriarCliente()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("FFBL (plugin Dalamud, 0.2)");
        return c;
    }

    private readonly object gate = new();
    private IDalamudTextureWrap? atual;
    private string urlAtual = "";
    private string baixando = "";
    private readonly List<IDalamudTextureWrap> lixo = new();

    /// <summary>Chamar no comeco de cada quadro: descarta texturas antigas com seguranca.</summary>
    public void Limpar()
    {
        List<IDalamudTextureWrap> descartar;
        lock (gate)
        {
            if (lixo.Count == 0) return;
            descartar = new List<IDalamudTextureWrap>(lixo);
            lixo.Clear();
        }
        foreach (var t in descartar) t.Dispose();
    }

    public IDalamudTextureWrap? Obter(string url)
    {
        lock (gate)
        {
            if (string.IsNullOrEmpty(url))
            {
                if (atual != null) { lixo.Add(atual); atual = null; }
                urlAtual = "";
                return null;
            }
            if (url != urlAtual && url != baixando)
            {
                baixando = url;
                _ = Task.Run(() => BaixarAsync(url));
            }
            return atual;
        }
    }

    private async Task BaixarAsync(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            var tex = await Plugin.TextureProvider.CreateFromImageAsync(bytes);
            lock (gate)
            {
                if (baixando != url) { lixo.Add(tex); return; } // ja pediram outra capa
                if (atual != null) lixo.Add(atual);
                atual = tex;
                urlAtual = url;
                baixando = "";
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "FFBL: nao consegui baixar a capa");
            lock (gate) { if (baixando == url) baixando = ""; urlAtual = url; /* nao tenta de novo ate mudar a musica */ }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (atual != null) lixo.Add(atual);
            atual = null;
        }
        Limpar();
    }
}
