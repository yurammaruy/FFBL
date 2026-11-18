using System.Text;

namespace FFBL;

/// <summary>
/// Codigo de conexao gerado pelo painel do bot (FFBL_Painel). Leva Client ID, Client Secret,
/// link do webhook e ID da sala de voz, para a pessoa colar um unico codigo.
/// Formato: "FFBL1-" + base64url( "id|segredo|webhookId/webhookToken|sala" + CRC32 de 4 bytes ).
/// </summary>
internal static class CodigoConexao
{
    public sealed record Dados(string ClientId, string ClientSecret, string WebhookUrl, string SalaId);

    private const string Prefixo = "FFBL1-";

    public static bool TentarLer(string codigo, out Dados? dados, out string erro)
    {
        dados = null; erro = "";
        var t = new string((codigo ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (!t.StartsWith(Prefixo, StringComparison.OrdinalIgnoreCase)) { erro = "Codigo invalido: ele comeca com FFBL1-."; return false; }
        t = t[Prefixo.Length..].Replace('-', '+').Replace('_', '/');
        t = t.PadRight(t.Length + (4 - t.Length % 4) % 4, '=');
        byte[] bytes;
        try { bytes = Convert.FromBase64String(t); }
        catch { erro = "Codigo invalido: faltou ou sobrou algum pedaco. Copie o codigo inteiro."; return false; }
        if (bytes.Length < 5) { erro = "Codigo invalido: muito curto."; return false; }

        var corpo = bytes[..^4];
        var crc = bytes[^4..];
        var calc = Crc32(corpo);
        if (crc[0] != (byte)(calc >> 24) || crc[1] != (byte)(calc >> 16) || crc[2] != (byte)(calc >> 8) || crc[3] != (byte)calc)
        { erro = "Codigo invalido: algum caractere esta errado. Copie o codigo inteiro de novo."; return false; }

        var partes = Encoding.UTF8.GetString(corpo).Split('|');
        if (partes.Length != 4) { erro = "Codigo invalido."; return false; }
        var url = "https://discord.com/api/webhooks/" + partes[2];
        if (!Webhook.Valido(url)) { erro = "Codigo invalido: o webhook dentro dele nao e valido."; return false; }
        dados = new Dados(partes[0], partes[1], url, partes[3]);
        return true;
    }

    private static uint Crc32(byte[] dados)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in dados)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
