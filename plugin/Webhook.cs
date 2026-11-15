using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FFBL;

internal static class Webhook
{
    private static readonly HttpClient Http = CriarCliente();
    private static readonly Regex Formato = new(@"^https://(discord|discordapp)\.com/api/webhooks/\d+/[\w-]+$", RegexOptions.Compiled);

    private static HttpClient CriarCliente()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("FFBL (plugin Dalamud, 0.1)");
        return c;
    }

    public static bool Valido(string url) => Formato.IsMatch((url ?? "").Trim());

    /// <summary>Pergunta ao Discord de qual sala e este webhook.</summary>
    public static async Task<string> ObterCanalAsync(string url, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url.Trim(), ct);
        if (!resp.IsSuccessStatusCode)
            throw new Exception("O Discord nao reconheceu o link do webhook (ele pode ter sido apagado).");
        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
        var canal = (string?)json?["channel_id"];
        if (string.IsNullOrEmpty(canal)) throw new Exception("O webhook nao informou a sala.");
        return canal;
    }

    private static string LimparNome(string nome)
    {
        nome = (nome ?? "").Trim();
        if (nome.Length == 0) nome = "Jogador";
        // o Discord recusa nomes de webhook com estas palavras
        nome = Regex.Replace(nome, "discord", "disc0rd", RegexOptions.IgnoreCase);
        nome = Regex.Replace(nome, "clyde", "cly0e", RegexOptions.IgnoreCase);
        if (nome.Length > 80) nome = nome[..80];
        return nome;
    }

    public static async Task EnviarAsync(string url, string nome, string avatarUrl, string texto, CancellationToken ct = default)
    {
        var corpo = new JsonObject
        {
            ["content"] = texto,
            ["username"] = LimparNome(nome),
            ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() },
        };
        if (!string.IsNullOrEmpty(avatarUrl)) corpo["avatar_url"] = avatarUrl;

        var json = corpo.ToJsonString();
        for (var tentativa = 0; tentativa < 4; tentativa++)
        {
            using var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(url.Trim() + "?wait=true", conteudo, ct);
            if (resp.IsSuccessStatusCode) return;

            var detalhe = await resp.Content.ReadAsStringAsync(ct);
            if ((int)resp.StatusCode == 429 && tentativa < 3)
            {
                // muitas mensagens seguidas: o Discord diz quanto esperar
                var espera = 1.0;
                try { espera = (double?)JsonNode.Parse(detalhe)?["retry_after"] ?? 1.0; } catch { /* usa 1 s */ }
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(espera, 0.2, 10)) + TimeSpan.FromMilliseconds(100), ct);
                continue;
            }
            throw new Exception($"O Discord recusou a mensagem ({(int)resp.StatusCode}). {detalhe}");
        }
    }

    /// <summary>Limite de envio de arquivos do Discord para webhooks (sem impulso no servidor).</summary>
    public const long LimiteArquivo = 10L * 1024 * 1024;

    private static readonly HashSet<string> ExtPermitidas = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    /// <summary>Diz se o arquivo pode ir para o chat do Discord (tipo e tamanho). Se nao, devolve o motivo.</summary>
    public static bool PodeEnviarArquivo(string caminho, out string motivo)
    {
        motivo = "";
        try
        {
            var fi = new FileInfo(caminho);
            if (!fi.Exists) { motivo = "arquivo nao encontrado"; return false; }
            if (!ExtPermitidas.Contains(fi.Extension)) { motivo = "o Discord nao aceita este tipo de arquivo no chat"; return false; }
            if (fi.Length > LimiteArquivo) { motivo = $"maior que {LimiteArquivo / 1024 / 1024} MB (limite do Discord)"; return false; }
            return true;
        }
        catch (Exception ex) { motivo = ex.Message; return false; }
    }

    /// <summary>Envia um arquivo para o chat e devolve o endereco dele no Discord.</summary>
    public static async Task<string> EnviarArquivoAsync(string url, string nome, string avatarUrl, string texto, string caminho, CancellationToken ct = default)
    {
        if (!PodeEnviarArquivo(caminho, out var motivo)) throw new Exception("Nao enviei o arquivo ao chat: " + motivo);

        var corpo = new JsonObject
        {
            ["content"] = texto,
            ["username"] = LimparNome(nome),
            ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() },
        };
        if (!string.IsNullOrEmpty(avatarUrl)) corpo["avatar_url"] = avatarUrl;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json"), "payload_json");
        var bytes = await File.ReadAllBytesAsync(caminho, ct);
        var parte = new ByteArrayContent(bytes);
        form.Add(parte, "files[0]", Path.GetFileName(caminho));

        using var cli = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        cli.DefaultRequestHeaders.UserAgent.ParseAdd("FFBL (plugin Dalamud, 0.9)");
        using var resp = await cli.PostAsync(url.Trim() + "?wait=true", form, ct);
        var texto2 = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new Exception($"O Discord recusou o arquivo ({(int)resp.StatusCode}). {texto2}");
        var link = (string?)JsonNode.Parse(texto2)?["attachments"]?[0]?["url"];
        if (string.IsNullOrEmpty(link)) throw new Exception("O Discord nao devolveu o endereco do arquivo.");
        return link;
    }
}
