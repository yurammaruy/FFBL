using Dalamud.Configuration;

namespace FFBL;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // App do Discord (o mesmo do OMNE serve)
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";

    // Link do webhook da sala de voz (funciona como senha: nao compartilhe)
    public string WebhookUrl { get; set; } = "";

    public bool ConectarAoAbrir { get; set; } = false;

    // Tamanho do iPod na tela (1.0 = tamanho original da imagem)
    public float Escala { get; set; } = 0.5f;

    // Icone flutuante (o "macro"): clique nele para abrir/fechar o FFBL
    public bool MostrarIcone { get; set; } = false;

    // O que aparece no telao (so para quem escolheu): 0 padrao, 1 capa da musica, 2 imagem local
    public int ProjFonte { get; set; } = 0;
    public string ProjImagemLocal { get; set; } = "";

    // Telao feito com o minion (precisa do Penumbra)
    // OMNE dentro do FFBL (ainda em desenvolvimento; padrao desligado)
    public string Idioma { get; set; } = "pt";   // pt (padrao), en, ja, de, fr
    public bool OmneAtivo { get; set; } = false;
    public OmneConfig Omne { get; set; } = new();

    public bool ProjUsarMinion { get; set; } = false;
    public bool ProjAceitarTelaoDeOutros { get; set; } = true;  // ver o telao de OUTRA pessoa (so visual: nao invoca nada no seu jogo)
    public uint ProjMinionId { get; set; } = 0;           // 0 = Plush Cushion
    public float ProjBaseLargura { get; set; } = 3f;      // tamanho natural da parede (calibragem)
    public float ProjBaseAltura { get; set; } = 3f;
    public float ProjGiroModelo { get; set; } = 0f;       // ajuste de giro do modelo, em graus
    public float ProjRotX { get; set; } = 0f;             // inclinar para frente/tras (so no seu jogo)
    public float ProjRotZ { get; set; } = 0f;             // inclinar para os lados (so no seu jogo)
    public float ProjEspessura { get; set; } = 1f;        // tamanho no eixo Z (so no seu jogo)
    public float ProjBrilho { get; set; } = 0f;           // luz propria da tela (0 = so a luz do ambiente)
    public bool ProjTeste { get; set; } = false;          // mostra a grade de teste no lugar da imagem
    public float ProjImgDx { get; set; } = 0f;            // ajuste da imagem dentro da tela
    public float ProjImgDy { get; set; } = 0f;
    public float ProjImgEx { get; set; } = 1f;
    public float ProjImgEy { get; set; } = 1f;
    public bool ProjCostasPretas { get; set; } = false;   // lado de tras preto (redesenha o minion ao cruzar)
    public bool ProjEspelharH { get; set; } = false;
    public bool ProjEspelharV { get; set; } = false;
}

/// <summary>Dados do OMNE (voz por proximidade). Ficam separados dos do FFBL.</summary>
[Serializable]
public sealed class OmneConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public bool ProximityEnabled { get; set; } = true;
    public float FullDistance { get; set; } = 5f;
    public float MaxDistance { get; set; } = 20f;
    public int MaxVolumePercent { get; set; } = 100;
    public Dictionary<string, string> Links { get; set; } = new();
}
