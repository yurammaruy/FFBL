using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace FFBL;

/// <summary>
/// Conversa com o app do Discord do proprio computador (named pipe discord-ipc-N).
/// Faz login, e fica recebendo as mensagens da sala escolhida.
/// </summary>
internal sealed class DiscordRpc
{
    private static readonly HttpClient Http = CriarCliente();

    private static HttpClient CriarCliente()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("FFBL (plugin Dalamud, 0.1)");
        return c;
    }

    private readonly Configuration cfg;
    private readonly Action saveConfig;
    private readonly ChatState chat;

    private NamedPipeClientStream? pipe;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonNode>> pending = new();
    private string myId = "";

    /// <summary>Disparado quando o login e a assinatura da sala deram certo.</summary>
    public event Action? Conectou;

    public DiscordRpc(Configuration cfg, Action saveConfig, ChatState chat)
    {
        this.cfg = cfg;
        this.saveConfig = saveConfig;
        this.chat = chat;
    }

    // ------------------------------------------------------------ pipe e frames

    private async Task ConectarPipeAsync(CancellationToken ct)
    {
        for (var i = 0; i <= 9; i++)
        {
            var p = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await p.ConnectAsync(500, ct);
                pipe = p;
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                p.Dispose();
                throw;
            }
            catch
            {
                p.Dispose();
            }
        }
        throw new Exception("Nao achei o Discord. Abra o aplicativo do Discord no computador (nao so no navegador).");
    }

    private async Task WriteFrameAsync(int op, string json, CancellationToken ct)
    {
        var p = pipe ?? throw new Exception("Sem conexao com o Discord.");
        var dados = Encoding.UTF8.GetBytes(json);
        var buf = new byte[8 + dados.Length];
        BitConverter.TryWriteBytes(buf.AsSpan(0, 4), op);
        BitConverter.TryWriteBytes(buf.AsSpan(4, 4), dados.Length);
        dados.CopyTo(buf, 8);
        await writeLock.WaitAsync(ct);
        try
        {
            await p.WriteAsync(buf, ct);
            await p.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task ReadExactAsync(byte[] buf, int count, CancellationToken ct)
    {
        var p = pipe ?? throw new Exception("Sem conexao com o Discord.");
        var lido = 0;
        while (lido < count)
        {
            var r = await p.ReadAsync(buf.AsMemory(lido, count - lido), ct);
            if (r <= 0) throw new Exception("A conexao com o Discord foi fechada.");
            lido += r;
        }
    }

    private async Task<(int Op, string Json)> ReadFrameAsync(CancellationToken ct)
    {
        var cab = new byte[8];
        await ReadExactAsync(cab, 8, ct);
        var op = BitConverter.ToInt32(cab, 0);
        var tam = BitConverter.ToInt32(cab, 4);
        if (tam < 0 || tam > 16 * 1024 * 1024) throw new Exception("Resposta estranha do Discord.");
        var corpo = new byte[tam];
        if (tam > 0) await ReadExactAsync(corpo, tam, ct);
        return (op, Encoding.UTF8.GetString(corpo));
    }

    // ------------------------------------------------------------ chamadas

    private async Task<JsonNode> CallAsync(string cmd, JsonObject args, string? evt, TimeSpan timeout, CancellationToken ct)
    {
        var nonce = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[nonce] = tcs;
        try
        {
            var corpo = new JsonObject { ["cmd"] = cmd, ["args"] = args, ["nonce"] = nonce };
            if (evt != null) corpo["evt"] = evt;
            await WriteFrameAsync(1, corpo.ToJsonString(), ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            JsonNode resp;
            try
            {
                resp = await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"O Discord nao respondeu ao pedido {cmd}.");
            }

            if ((string?)resp["evt"] == "ERROR")
            {
                var codigo = resp["data"]?["code"]?.ToString();
                var msg = (string?)resp["data"]?["message"];
                throw new Exception($"O Discord recusou {cmd} (codigo {codigo}: {msg}).");
            }
            return resp;
        }
        finally
        {
            pending.TryRemove(nonce, out _);
        }
    }

    // ------------------------------------------------------------ rodar

    /// <summary>Conecta, faz login e fica ouvindo a sala ate ser cancelado ou dar erro.</summary>
    public async Task RunAsync(string channelId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            throw new Exception("Falta o Client ID ou o Client Secret.");

        chat.SetStatus("Procurando o Discord...", 1);
        await ConectarPipeAsync(ct);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task? leitor = null;
        try
        {
            // aperto de mao
            await WriteFrameAsync(0, new JsonObject { ["v"] = 1, ["client_id"] = cfg.ClientId.Trim() }.ToJsonString(), linked.Token);
            while (true)
            {
                var (op, json) = await ReadFrameAsync(linked.Token);
                if (op == 3) { await WriteFrameAsync(4, json, linked.Token); continue; }
                if (op == 2) throw new Exception("O Discord recusou o aplicativo: " + MensagemDeErro(json));
                var no = string.IsNullOrEmpty(json) ? null : JsonNode.Parse(json);
                if ((string?)no?["evt"] == "READY") break;
            }

            leitor = LerEmLoopAsync(linked.Token);

            var user = await AutenticarAsync(linked.Token);
            myId = (string?)user["id"] ?? "";
            var nome = (string?)user["global_name"];
            if (string.IsNullOrWhiteSpace(nome)) nome = (string?)user["username"] ?? "Jogador";
            var avatar = (string?)user["avatar"];
            var avatarUrl = string.IsNullOrEmpty(avatar) || string.IsNullOrEmpty(myId)
                ? ""
                : $"https://cdn.discordapp.com/avatars/{myId}/{avatar}.png?size=128";
            chat.SetMe(myId, nome!, avatarUrl);

            chat.SetStatus("Entrando na sala...", 1);
            await CallAsync("SUBSCRIBE", new JsonObject { ["channel_id"] = channelId }, "MESSAGE_CREATE", TimeSpan.FromSeconds(15), linked.Token);
            await CallAsync("SUBSCRIBE", new JsonObject { ["channel_id"] = channelId }, "MESSAGE_DELETE", TimeSpan.FromSeconds(15), linked.Token);
            await CallAsync("SUBSCRIBE", new JsonObject { ["channel_id"] = channelId }, "MESSAGE_UPDATE", TimeSpan.FromSeconds(15), linked.Token);
            Conectou?.Invoke();
            chat.SetStatus("Conectado como " + nome, 2);

            await leitor; // fica aqui ate cair ou cancelarem
        }
        finally
        {
            linked.Cancel();
            foreach (var kv in pending) kv.Value.TrySetCanceled();
            try { pipe?.Dispose(); } catch { /* ignora */ }
            pipe = null;
            if (leitor != null)
            {
                try { await leitor; } catch { /* ja tratado */ }
            }
        }
    }

    private async Task<JsonNode> AutenticarAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(cfg.AccessToken))
        {
            try
            {
                return await AutenticarComTokenAsync(cfg.AccessToken, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // token velho ou sem a permissao de ler mensagens: pede de novo
                cfg.AccessToken = "";
                saveConfig();
            }
        }

        chat.SetStatus("Olhe a janela do Discord e clique em Autorizar...", 1);
        var r = await CallAsync(
            "AUTHORIZE",
            new JsonObject
            {
                ["client_id"] = cfg.ClientId.Trim(),
                ["scopes"] = new JsonArray("rpc", "identify", "messages.read"),
            },
            null, TimeSpan.FromMinutes(3), ct);
        var code = (string?)r["data"]?["code"];
        if (string.IsNullOrEmpty(code)) throw new Exception("O Discord nao devolveu o codigo de autorizacao.");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = "http://localhost",
            ["client_id"] = cfg.ClientId.Trim(),
            ["client_secret"] = cfg.ClientSecret.Trim(),
        });
        using var resp = await Http.PostAsync("https://discord.com/api/oauth2/token", form, ct);
        var texto = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new Exception("Nao consegui trocar o codigo pelo token (confira o Client Secret). " + texto);
        var token = (string?)JsonNode.Parse(texto)?["access_token"];
        if (string.IsNullOrEmpty(token)) throw new Exception("O Discord nao devolveu o token.");

        cfg.AccessToken = token;
        saveConfig();
        return await AutenticarComTokenAsync(token, ct);
    }

    private async Task<JsonNode> AutenticarComTokenAsync(string token, CancellationToken ct)
    {
        var r = await CallAsync("AUTHENTICATE", new JsonObject { ["access_token"] = token }, null, TimeSpan.FromSeconds(15), ct);
        return r["data"]?["user"] ?? throw new Exception("O Discord nao informou quem voce e.");
    }

    // ------------------------------------------------------------ receber

    private async Task LerEmLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var (op, json) = await ReadFrameAsync(ct);
                if (op == 3) { await WriteFrameAsync(4, json, ct); continue; }
                if (op == 2) throw new Exception("O Discord fechou a conexao: " + MensagemDeErro(json));
                if (string.IsNullOrEmpty(json)) continue;

                JsonNode? no;
                try { no = JsonNode.Parse(json); } catch { continue; }
                if (no == null) continue;

                var nonce = (string?)no["nonce"];
                if (nonce != null && pending.TryRemove(nonce, out var tcs))
                {
                    tcs.TrySetResult(no);
                    continue;
                }

                var evt = (string?)no["evt"];
                if (evt == "MESSAGE_CREATE") TratarMensagem(no["data"]?["message"]);
                else if (evt == "MESSAGE_UPDATE") TratarPainel(no["data"]?["message"]);
                else if (evt == "MESSAGE_DELETE")
                {
                    var id = (string?)no["data"]?["message"]?["id"];
                    if (!string.IsNullOrEmpty(id)) chat.Remove(id);
                }
            }
        }
        catch (Exception ex)
        {
            foreach (var kv in pending) kv.Value.TrySetException(ex);
            if (ct.IsCancellationRequested) return;
            throw;
        }
    }

    private void TratarPainel(JsonNode? m)
    {
        var embeds = m?["embeds"] as JsonArray;
        if (embeds == null) return;
        foreach (var e in embeds)
        {
            if ((string?)e?["footer"]?["text"] != "Painel JBLEZA") continue;
            var estado = (string?)e["author"]?["name"] ?? "Parado";
            var titulo = (string?)e["title"] ?? "";
            var url = (string?)e["url"] ?? "";
            var thumb = (string?)e["thumbnail"]?["url"] ?? "";
            var fila = new List<string>();
            var desc = (string?)e["description"] ?? "";
            foreach (var bruta in desc.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var linha = bruta.Trim();
                if (linha.StartsWith("...")) continue;
                var ponto = linha.IndexOf(". ", StringComparison.Ordinal);
                if (ponto > 0 && ponto <= 3 && int.TryParse(linha[..ponto], out _)) linha = linha[(ponto + 2)..];
                var corte = linha.LastIndexOf(" · pedido de ", StringComparison.Ordinal);
                if (corte > 0) linha = linha[..corte];
                fila.Add(linha);
            }
            ulong.TryParse((string?)m?["id"], out var id);

            var volume = 100;
            TelaoInfo? telao = null;
            VideoInfo? video = null;
            long musicaInicio = 0;
            if (e["fields"] is JsonArray campos)
            {
                foreach (var c in campos)
                {
                    var nomeCampo = (string?)c?["name"];
                    var valor = (string?)c?["value"] ?? "";
                    if (nomeCampo == "volume") int.TryParse(valor, out volume);
                    else if (nomeCampo == "telao") telao = LerTelao(valor);
                    else if (nomeCampo == "video") video = LerVideo(valor);
                    else if (nomeCampo == "inicio") long.TryParse(valor, out musicaInicio);
                }
            }
            chat.SetPanel(new PanelInfo(estado, estado == "Parado" ? "" : titulo, url, thumb, fila, id, volume, telao, video, musicaInicio));
            return;
        }
    }

    /// <summary>Le o texto "hora_de_inicio | endereco | quem".</summary>
    private static VideoInfo? LerVideo(string valor)
    {
        var p = valor.Split('|', 3, StringSplitOptions.TrimEntries);
        if (p.Length < 2 || !long.TryParse(p[0], out var ini)) return null;
        return new VideoInfo(ini, p[1], p.Length > 2 ? p[2] : "");
    }

    /// <summary>Le o texto "territorio | x | y | z | largura | altura | giro | dono".</summary>
    private static TelaoInfo? LerTelao(string valor)
    {
        var p = valor.Split('|', StringSplitOptions.TrimEntries);
        if (p.Length < 8) return null;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        var n = System.Globalization.NumberStyles.Float;
        if (!uint.TryParse(p[0], out var terr)
            || !float.TryParse(p[1], n, c, out var x) || !float.TryParse(p[2], n, c, out var y)
            || !float.TryParse(p[3], n, c, out var z) || !float.TryParse(p[4], n, c, out var w)
            || !float.TryParse(p[5], n, c, out var h) || !float.TryParse(p[6], n, c, out var g))
            return null;
        return new TelaoInfo(terr, new System.Numerics.Vector3(x, y, z), w, h, g, p[7]);
    }

    private void TratarMensagem(JsonNode? m)
    {
        if (m == null) return;
        TratarPainel(m);
        var id = (string?)m["id"] ?? Guid.NewGuid().ToString();
        var texto = (string?)m["content"] ?? "";
        if (string.IsNullOrWhiteSpace(texto)) return;

        var autor = m["author"];
        var nome = (string?)m["nick"];
        if (string.IsNullOrWhiteSpace(nome)) nome = (string?)autor?["global_name"];
        if (string.IsNullOrWhiteSpace(nome)) nome = (string?)autor?["username"] ?? "?";

        var autorId = (string?)autor?["id"] ?? "";
        var ehBot = autor?["bot"]?.GetValue<bool>() ?? false;
        var viaWebhook = m["webhook_id"] != null;
        // resposta do bot de musica = mensagem de bot que NAO veio por webhook
        var respostaDoBot = ehBot && !viaWebhook;
        var meuNome = chat.Me.Name;
        var minha = (!string.IsNullOrEmpty(myId) && autorId == myId)
                    || (!viaWebhook ? false : string.Equals(nome, meuNome, StringComparison.OrdinalIgnoreCase));

        chat.Add(new ChatLine(id, nome!, texto, respostaDoBot, minha, DateTime.Now));
    }

    private static string MensagemDeErro(string json)
    {
        try { return (string?)JsonNode.Parse(json)?["message"] ?? "sem detalhes"; }
        catch { return "sem detalhes"; }
    }
}
