using System.Runtime.InteropServices;
using System.Text.Json;

namespace FFBL;

/// <summary>
/// Guarda os dados de conexao (Client ID/Secret, webhook, sala, autorizacoes) num arquivo
/// oculto e criptografado pelo proprio Windows (DPAPI): so a mesma conta de usuario, neste PC,
/// consegue abrir. Nao ha senha escrita em lugar nenhum. O arquivo e apagado quando o personagem
/// desconecta, ao abrir o jogo e ao descarregar o plugin.
/// </summary>
internal static class CofreConexao
{
    private sealed class Dados
    {
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string WebhookUrl { get; set; } = "";
        public string OmneClientId { get; set; } = "";
        public string OmneClientSecret { get; set; } = "";
        public string OmneAccessToken { get; set; } = "";
        public string OmneChannelId { get; set; } = "";
    }

    private static string Pasta => Plugin.PluginInterface.ConfigDirectory.FullName;
    private static string Caminho => Path.Combine(Pasta, "conexao.dat");

    public static void Salvar(Configuration c)
    {
        try
        {
            var d = new Dados
            {
                ClientId = c.ClientId, ClientSecret = c.ClientSecret, AccessToken = c.AccessToken, WebhookUrl = c.WebhookUrl,
                OmneClientId = c.Omne.ClientId, OmneClientSecret = c.Omne.ClientSecret,
                OmneAccessToken = c.Omne.AccessToken, OmneChannelId = c.Omne.ChannelId,
            };
            var tudo = d.ClientId + d.ClientSecret + d.AccessToken + d.WebhookUrl
                       + d.OmneClientId + d.OmneClientSecret + d.OmneAccessToken + d.OmneChannelId;
            if (tudo.Length == 0) { Apagar(); return; }

            var cifrado = Aplicar(JsonSerializer.SerializeToUtf8Bytes(d), true);
            Directory.CreateDirectory(Pasta);
            Apagar();
            File.WriteAllBytes(Caminho, cifrado);
            File.SetAttributes(Caminho, FileAttributes.Hidden | FileAttributes.System);
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "FFBL: nao consegui guardar os dados de conexao"); }
    }

    public static void Carregar(Configuration c)
    {
        try
        {
            if (!File.Exists(Caminho)) return;
            var d = JsonSerializer.Deserialize<Dados>(Aplicar(File.ReadAllBytes(Caminho), false));
            if (d == null) return;
            c.ClientId = d.ClientId; c.ClientSecret = d.ClientSecret; c.AccessToken = d.AccessToken; c.WebhookUrl = d.WebhookUrl;
            c.Omne.ClientId = d.OmneClientId; c.Omne.ClientSecret = d.OmneClientSecret;
            c.Omne.AccessToken = d.OmneAccessToken; c.Omne.ChannelId = d.OmneChannelId;
        }
        catch
        {
            Apagar(); // ilegivel (outra conta ou outro PC): descarta
        }
    }

    public static void Apagar()
    {
        try
        {
            if (!File.Exists(Caminho)) return;
            File.SetAttributes(Caminho, FileAttributes.Normal);
            File.Delete(Caminho);
        }
        catch { /* ignora */ }
    }

    // ------------------------------------------------------------ DPAPI (crypt32.dll)

    [StructLayout(LayoutKind.Sequential)]
    private struct Bloco { public int Tamanho; public IntPtr Dados; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref Bloco entrada, string? descricao, IntPtr entropia, IntPtr reservado,
        IntPtr prompt, int flags, out Bloco saida);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref Bloco entrada, IntPtr descricao, IntPtr entropia, IntPtr reservado,
        IntPtr prompt, int flags, out Bloco saida);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr h);

    private static byte[] Aplicar(byte[] bytes, bool proteger)
    {
        var entrada = new Bloco { Tamanho = bytes.Length, Dados = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, entrada.Dados, bytes.Length);
            Bloco saida;
            var ok = proteger
                ? CryptProtectData(ref entrada, "FFBL", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out saida)
                : CryptUnprotectData(ref entrada, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out saida);
            if (!ok) throw new InvalidOperationException("DPAPI falhou: " + Marshal.GetLastWin32Error());
            try
            {
                var r = new byte[saida.Tamanho];
                Marshal.Copy(saida.Dados, r, 0, saida.Tamanho);
                return r;
            }
            finally { LocalFree(saida.Dados); }
        }
        finally { Marshal.FreeHGlobal(entrada.Dados); }
    }
}
