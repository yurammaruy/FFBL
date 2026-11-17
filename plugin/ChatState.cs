using System.Numerics;

namespace FFBL;

internal sealed record TelaoInfo(uint Territorio, Vector3 Pos, float Largura, float Altura, float Giro, string Dono);

/// <summary>Video exibido no telao por alguem (o bot guarda o endereco e a hora em que comecou).</summary>
internal sealed record VideoInfo(long InicioEpoch, string Url, string Quem);

internal sealed record PanelInfo(string Estado, string Titulo, string Url, string Thumb, List<string> Fila, ulong MsgId, int Volume, TelaoInfo? Telao, VideoInfo? Video = null, long MusicaInicio = 0, string BotId = "");

internal sealed record ChatLine(string Id, string Author, string Text, bool IsBotReply, bool IsMine, DateTime Time);

/// <summary>Estado compartilhado entre a conexao (outra thread) e a janela. Tudo protegido por lock.</summary>
internal sealed class ChatState
{
    private readonly object gate = new();
    private readonly List<ChatLine> lines = new();
    private const int MaxLines = 200;

    private string status = "Desconectado";
    private int statusKind; // 0 cinza, 1 amarelo, 2 verde, 3 vermelho
    private string nowPlaying = "";
    private string myId = "", myName = "", myAvatarUrl = "";
    private int version;
    private PanelInfo? panel;
    private string lastReply = "";
    private DateTime lastReplyAt = DateTime.MinValue;

    public int Version { get { lock (gate) return version; } }

    public (string Text, int Kind) Status { get { lock (gate) return (status, statusKind); } }
    public string NowPlaying { get { lock (gate) return nowPlaying; } }
    public (string Id, string Name, string AvatarUrl) Me { get { lock (gate) return (myId, myName, myAvatarUrl); } }

    public PanelInfo? Panel { get { lock (gate) return panel; } }
    public (string Text, DateTime At) LastReply { get { lock (gate) return (lastReply, lastReplyAt); } }

    /// <summary>Aceita o painel mais novo (id maior); o mesmo id atualiza.</summary>
    public void SetPanel(PanelInfo p)
    {
        lock (gate)
        {
            if (panel != null && p.MsgId < panel.MsgId) return;
            panel = p;
            version++;
        }
    }

    public void SetStatus(string text, int kind)
    {
        lock (gate) { status = text; statusKind = kind; version++; }
    }

    public void SetMe(string id, string name, string avatarUrl)
    {
        lock (gate) { myId = id; myName = name; myAvatarUrl = avatarUrl; version++; }
    }

    public void Clear()
    {
        lock (gate) { lines.Clear(); nowPlaying = ""; panel = null; lastReply = ""; version++; }
    }

    public void Add(ChatLine line)
    {
        lock (gate)
        {
            if (lines.Any(l => l.Id == line.Id)) return;
            lines.Add(line);
            if (lines.Count > MaxLines) lines.RemoveAt(0);
            if (line.IsBotReply) { lastReply = line.Text.Replace("**", "").Replace("`", ""); lastReplyAt = DateTime.Now; }

            if (line.Text.StartsWith("Tocando agora:", StringComparison.OrdinalIgnoreCase))
            {
                // "Tocando agora: **Titulo** [3:20] - pedido de Fulano"
                nowPlaying = line.Text["Tocando agora:".Length..].Replace("**", "").Trim();
            }
            else if (line.Text.StartsWith("Parei a musica", StringComparison.OrdinalIgnoreCase)
                     || line.Text.StartsWith("Saindo da sala", StringComparison.OrdinalIgnoreCase))
            {
                nowPlaying = "";
            }
            version++;
        }
    }

    public void Remove(string id)
    {
        lock (gate)
        {
            if (lines.RemoveAll(l => l.Id == id) > 0) version++;
        }
    }

    public List<ChatLine> Snapshot()
    {
        lock (gate) return new List<ChatLine>(lines);
    }
}
