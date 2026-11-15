using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Dalamud.Plugin.Services;

namespace FFBL;

internal sealed record OmneMembro(string Id, string Name);

// Conversa com o Discord instalado NESTE computador (canal local, sem internet),
// exceto a troca única da autorização, que fala com o próprio Discord.
internal sealed class OmneRpc : IDisposable
{
    private readonly OmneConfig config;
    private readonly Action saveConfig;
    private readonly IPluginLog log;
    private readonly HttpClient http = new();
    private readonly object gate = new();

    // Só uma conversa por vez no canal local do Discord.
    private readonly SemaphoreSlim pipeLock = new(1, 1);

    private CancellationTokenSource? cts;
    private NamedPipeClientStream? pipe;

    private List<OmneMembro> members = new();
    private string status = "Desconectado";
    private string notice = "";
    private string volumeNotice = "";
    private string myId = "";
    private string channelName = "";
    private bool connected;
    private bool running;
    private bool inRoom;
    private volatile bool joinRequested;
    private volatile bool resetAllRequested;
    private DateTime volumeRetryAt = DateTime.MinValue;

    // Volume que o jogo calculou para cada pessoa (ID do Discord -> 0..200).
    private volatile IReadOnlyDictionary<string, int> desired = new Dictionary<string, int>();

    // Último volume realmente enviado ao Discord para cada pessoa.
    private readonly ConcurrentDictionary<string, int> lastSent = new();

    public OmneRpc(OmneConfig config, Action saveConfig, IPluginLog log)
    {
        this.config = config;
        this.saveConfig = saveConfig;
        this.log = log;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FFBL-OMNE-DalamudPlugin");
    }

    public string Status { get { lock (gate) return status; } }
    public string Notice { get { lock (gate) return notice; } }
    public string VolumeNotice { get { lock (gate) return volumeNotice; } }
    public bool Connected { get { lock (gate) return connected; } }
    public bool Running { get { lock (gate) return running; } }
    public bool InRoom { get { lock (gate) return inRoom; } }
    public string MyId { get { lock (gate) return myId; } }
    public string ChannelName { get { lock (gate) return channelName; } }
    public List<OmneMembro> Members { get { lock (gate) return new List<OmneMembro>(members); } }

    public int? GetApplied(string userId) => lastSent.TryGetValue(userId, out var v) ? v : null;

    private void SetStatus(string text) { lock (gate) status = text; }
    private void SetNotice(string text) { lock (gate) notice = text; }
    private void SetVolumeNotice(string text) { lock (gate) volumeNotice = text; }

    public void RequestJoin() { joinRequested = true; }
    public void RequestResetAll() { resetAllRequested = true; }
    public void SetDesired(IReadOnlyDictionary<string, int> volumes) { desired = volumes; }

    public void Start()
    {
        Stop();
        var source = new CancellationTokenSource();
        cts = source;
        lock (gate) { running = true; status = "Conectando ao Discord..."; notice = ""; volumeNotice = ""; }
        _ = Task.Run(() => RunAsync(source.Token));
    }

    public void Stop()
    {
        // Antes de fechar, devolve o volume de todo mundo para 100%.
        RestoreBlocking();

        var source = cts;
        cts = null;
        if (source != null)
        {
            try { source.Cancel(); } catch { }
        }
        ClosePipe();
        lock (gate)
        {
            running = false;
            connected = false;
            inRoom = false;
            members = new List<OmneMembro>();
            channelName = "";
            notice = "";
            volumeNotice = "";
            status = "Desconectado";
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
            {
                SetStatus("Preencha o Client ID e o Client Secret.");
                lock (gate) running = false;
                return;
            }

            await ConnectPipeAsync(ct);

            var hello = new JsonObject { ["v"] = 1, ["client_id"] = config.ClientId };
            await WriteFrameAsync(0, hello.ToJsonString(), ct);
            await ReadFrameAsync(ct); // resposta READY (ou erro, tratado dentro)

            await AuthenticateAsync(ct);

            lock (gate) connected = true;
            SetStatus("Conectado ao Discord");

            var lastRefresh = DateTime.MinValue;
            while (!ct.IsCancellationRequested)
            {
                if (joinRequested)
                {
                    joinRequested = false;
                    await JoinRoomAsync(ct);
                    lastRefresh = DateTime.MinValue;
                }

                var now = DateTime.UtcNow;
                if (now - lastRefresh >= TimeSpan.FromSeconds(3))
                {
                    await RefreshMembersAsync(ct);
                    lastRefresh = now;
                }

                await ApplyVolumesAsync(ct);
                await Task.Delay(250, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            log.Error(ex, "OMNE: falha na conexão com o Discord");
            lock (gate)
            {
                connected = false;
                running = false;
                inRoom = false;
                status = "Erro: " + ex.Message;
            }
            ClosePipe();
        }
    }

    // ------------------------------------------------------------------
    //  VOLUME POR DISTÂNCIA
    // ------------------------------------------------------------------

    private bool NeedsUpdate(int have, int want)
    {
        if (have == want) return false;
        // Nos extremos (silêncio e volume máximo) o valor precisa ser exato.
        if (want <= 0 || want >= config.MaxVolumePercent) return true;
        return Math.Abs(have - want) >= 2;
    }

    private async Task SetUserVolumeAsync(string userId, int percent, CancellationToken ct)
    {
        var args = new JsonObject { ["user_id"] = userId, ["volume"] = percent };
        await RequestAsync("SET_USER_VOICE_SETTINGS", args, ct);
    }

    private async Task ApplyVolumesAsync(CancellationToken ct)
    {
        var enabled = config.ProximityEnabled;
        bool here;
        List<OmneMembro> list;
        string me;
        lock (gate) { here = inRoom; list = members; me = myId; }

        if (resetAllRequested)
        {
            resetAllRequested = false;
            await ResetEveryoneAsync(list, me, ct);
        }

        // Proximidade desligada, ou você saiu da sala: devolve tudo ao normal.
        if (!enabled || !here)
        {
            await RestoreTouchedAsync(ct);
            SetVolumeNotice("");
            return;
        }

        if (DateTime.UtcNow < volumeRetryAt) return;

        var snapshot = desired;
        var present = new HashSet<string>(list.Select(m => m.Id));
        foreach (var stale in lastSent.Keys.Where(k => !present.Contains(k)).ToList())
            lastSent.TryRemove(stale, out _);

        try
        {
            foreach (var m in list)
            {
                if (m.Id == me) continue;

                if (!snapshot.TryGetValue(m.Id, out var want))
                {
                    // Sem vínculo: se já mexemos nessa pessoa, devolve ao normal.
                    if (lastSent.ContainsKey(m.Id))
                    {
                        await SetUserVolumeAsync(m.Id, 100, ct);
                        lastSent.TryRemove(m.Id, out _);
                    }
                    continue;
                }

                if (lastSent.TryGetValue(m.Id, out var have) && !NeedsUpdate(have, want))
                    continue;

                await SetUserVolumeAsync(m.Id, want, ct);
                lastSent[m.Id] = want;
            }
            SetVolumeNotice("");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            SetVolumeNotice("Não consegui ajustar o volume: " + ex.Message);
            volumeRetryAt = DateTime.UtcNow.AddSeconds(5);
        }
    }

    private async Task RestoreTouchedAsync(CancellationToken ct)
    {
        foreach (var id in lastSent.Keys.ToList())
        {
            try { await SetUserVolumeAsync(id, 100, ct); }
            catch (OperationCanceledException) { throw; }
            catch { }
            lastSent.TryRemove(id, out _);
        }
    }

    private async Task ResetEveryoneAsync(List<OmneMembro> list, string me, CancellationToken ct)
    {
        foreach (var m in list)
        {
            if (m.Id == me) continue;
            try { await SetUserVolumeAsync(m.Id, 100, ct); }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
        lastSent.Clear();
    }

    // Usado ao desconectar ou fechar o plugin: tenta, por até ~2 s, devolver 100%.
    private void RestoreBlocking()
    {
        if (lastSent.IsEmpty || pipe == null)
        {
            lastSent.Clear();
            return;
        }

        var ids = lastSent.Keys.ToList();
        try
        {
            Task.Run(async () =>
            {
                using var c = new CancellationTokenSource(1500);
                await pipeLock.WaitAsync(c.Token);
                try
                {
                    foreach (var id in ids)
                    {
                        var args = new JsonObject { ["user_id"] = id, ["volume"] = 100 };
                        await RequestCoreAsync("SET_USER_VOICE_SETTINGS", args, c.Token);
                    }
                }
                finally { pipeLock.Release(); }
            }).Wait(2000);
        }
        catch
        {
            // Se o Discord já fechou, não há o que restaurar.
        }
        lastSent.Clear();
    }

    // ------------------------------------------------------------------
    //  CONEXÃO E AUTORIZAÇÃO
    // ------------------------------------------------------------------

    private async Task ConnectPipeAsync(CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var p = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await p.ConnectAsync(500, ct);
                pipe = p;
                return;
            }
            catch (TimeoutException) { p.Dispose(); }
            catch (IOException) { p.Dispose(); }
        }
        throw new Exception("Não achei o Discord aberto neste computador. Abra o aplicativo do Discord (não o navegador) e tente de novo.");
    }

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(config.AccessToken))
        {
            try
            {
                var r = await RequestAsync("AUTHENTICATE", new JsonObject { ["access_token"] = config.AccessToken }, ct);
                StoreUser(r);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log.Information("OMNE: autorização guardada recusada ({0}); pedindo de novo.", ex.Message);
                config.AccessToken = "";
                saveConfig();
            }
        }

        SetStatus("Aprove a janela que apareceu no Discord...");
        var scopes = new JsonArray();
        scopes.Add("rpc");
        scopes.Add("identify");
        var auth = await RequestAsync("AUTHORIZE", new JsonObject { ["client_id"] = config.ClientId, ["scopes"] = scopes }, ct);
        var code = auth?["data"]?["code"]?.GetValue<string>();
        if (string.IsNullOrEmpty(code))
            throw new Exception("O Discord não devolveu o código de autorização.");

        SetStatus("Finalizando a autorização...");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = "http://localhost",
        });
        using var resp = await http.PostAsync("https://discord.com/api/oauth2/token", content, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"O Discord recusou a autorização (código {(int)resp.StatusCode}). Confira o Client Secret e se o endereço http://localhost está nos Redirects do aplicativo.");

        var token = JsonNode.Parse(text)?["access_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token))
            throw new Exception("O Discord não devolveu a chave de acesso.");

        config.AccessToken = token;
        saveConfig();

        var r2 = await RequestAsync("AUTHENTICATE", new JsonObject { ["access_token"] = token }, ct);
        StoreUser(r2);
    }

    private void StoreUser(JsonNode? response)
    {
        var id = response?["data"]?["user"]?["id"]?.GetValue<string>() ?? "";
        lock (gate) myId = id;
    }

    // Entra na sala escolhida (a do OMNÉ), saindo de outra sala se for preciso.
    private async Task JoinRoomAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(config.ChannelId))
        {
            SetNotice("Cole primeiro o ID da sala do OMNÉ.");
            return;
        }

        try
        {
            await RequestAsync("SELECT_VOICE_CHANNEL",
                new JsonObject { ["channel_id"] = config.ChannelId, ["force"] = true }, ct);
            SetNotice("");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            SetNotice("Não consegui entrar na sala: " + ex.Message);
        }
    }

    // Só lê as pessoas se você estiver NA sala escolhida. Qualquer outra sala é ignorada.
    private async Task RefreshMembersAsync(CancellationToken ct)
    {
        var r = await RequestAsync("GET_SELECTED_VOICE_CHANNEL", new JsonObject(), ct);
        var target = config.ChannelId;
        var list = new List<OmneMembro>();
        var chName = "";
        var here = false;
        string state;

        if (string.IsNullOrEmpty(target))
        {
            state = "Conectado. Cole o ID da sala do OMNÉ abaixo.";
        }
        else if (r?["data"] is JsonObject d)
        {
            var currentId = d["id"]?.GetValue<string>() ?? "";
            chName = d["name"]?.GetValue<string>() ?? "";

            if (currentId == target)
            {
                here = true;
                state = "Na sala do OMNÉ: " + chName;

                if (d["voice_states"] is JsonArray arr)
                {
                    foreach (var vs in arr)
                    {
                        var user = vs?["user"];
                        var id = user?["id"]?.GetValue<string>();
                        if (string.IsNullOrEmpty(id)) continue;

                        var name = vs?["nick"]?.GetValue<string>();
                        if (string.IsNullOrEmpty(name)) name = user?["global_name"]?.GetValue<string>();
                        if (string.IsNullOrEmpty(name)) name = user?["username"]?.GetValue<string>();
                        list.Add(new OmneMembro(id, name ?? id));
                    }
                }
            }
            else
            {
                list.Clear();
                state = "Conectado, mas você está em outra sala (" + chName + "). O OMNÉ não faz nada fora da sala escolhida.";
            }
        }
        else
        {
            state = "Conectado, mas você não está em nenhuma sala de voz.";
        }

        lock (gate)
        {
            members = list;
            channelName = chName;
            inRoom = here;
            status = state;
        }
    }

    // ------------------------------------------------------------------
    //  CANAL LOCAL (baixo nível)
    // ------------------------------------------------------------------

    private async Task<JsonNode?> RequestAsync(string cmd, JsonObject args, CancellationToken ct)
    {
        await pipeLock.WaitAsync(ct);
        try
        {
            return await RequestCoreAsync(cmd, args, ct);
        }
        finally
        {
            pipeLock.Release();
        }
    }

    private async Task<JsonNode?> RequestCoreAsync(string cmd, JsonObject args, CancellationToken ct)
    {
        var nonce = Guid.NewGuid().ToString();
        var msg = new JsonObject { ["cmd"] = cmd, ["args"] = args, ["nonce"] = nonce };
        await WriteFrameAsync(1, msg.ToJsonString(), ct);

        while (true)
        {
            var (op, node) = await ReadFrameAsync(ct);
            if (op == 3)
            {
                await WriteFrameAsync(4, node?.ToJsonString() ?? "{}", ct);
                continue;
            }
            if (node == null) continue;
            if (node["nonce"]?.GetValue<string>() != nonce) continue;
            if (node["evt"]?.GetValue<string>() == "ERROR")
                throw new Exception(node["data"]?["message"]?.GetValue<string>() ?? "O Discord devolveu um erro.");
            return node;
        }
    }

    private async Task WriteFrameAsync(int op, string json, CancellationToken ct)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var buf = new byte[8 + payload.Length];
        BitConverter.TryWriteBytes(buf.AsSpan(0, 4), op);
        BitConverter.TryWriteBytes(buf.AsSpan(4, 4), payload.Length);
        payload.CopyTo(buf, 8);
        await pipe!.WriteAsync(buf, ct);
        await pipe.FlushAsync(ct);
    }

    private async Task<(int op, JsonNode? node)> ReadFrameAsync(CancellationToken ct)
    {
        var header = new byte[8];
        await ReadExactAsync(header, ct);
        var op = BitConverter.ToInt32(header, 0);
        var len = BitConverter.ToInt32(header, 4);
        var body = new byte[len];
        await ReadExactAsync(body, ct);

        JsonNode? node = null;
        if (len > 0)
        {
            try { node = JsonNode.Parse(Encoding.UTF8.GetString(body)); }
            catch { node = null; }
        }

        if (op == 2)
        {
            var msg = node?["message"]?.GetValue<string>() ?? "conexão encerrada";
            throw new Exception("O Discord recusou a conexão: " + msg);
        }
        return (op, node);
    }

    private async Task ReadExactAsync(byte[] buf, CancellationToken ct)
    {
        var read = 0;
        while (read < buf.Length)
        {
            var n = await pipe!.ReadAsync(buf.AsMemory(read), ct);
            if (n == 0) throw new Exception("O Discord fechou a conexão.");
            read += n;
        }
    }

    private void ClosePipe()
    {
        try { pipe?.Dispose(); } catch { }
        pipe = null;
    }

    public void Dispose()
    {
        Stop();
        http.Dispose();
    }
}
