using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FFBL;

/// <summary>Icone flutuante (o "macro"): clique abre/fecha o FFBL; arraste para colocar onde quiser.</summary>
internal sealed class IconWindow : Window
{
    private readonly Plugin plugin;
    private bool arrastou;
    private bool estilo;
    private const float Lado = 52f;

    public IconWindow(Plugin plugin) : base("FFBL icone###FFBL_Icone")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoFocusOnAppearing;
        RespectCloseHotkey = false;
        Size = new Vector2(Lado, Lado);
        SizeCondition = ImGuiCond.Always;
        IsOpen = plugin.Config.MostrarIcone;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        estilo = true;
    }

    public override void PostDraw()
    {
        if (estilo) { ImGui.PopStyleVar(); estilo = false; }
    }

    public override void Draw()
    {
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var caminho = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? "", "images", "macro.png");
        var tex = Plugin.TextureProvider.GetFromFile(caminho).GetWrapOrEmpty();
        dl.AddImage(tex.Handle, pos, pos + new Vector2(Lado, Lado));

        ImGui.InvisibleButton("##icone", new Vector2(Lado, Lado));
        if (ImGui.IsItemHovered())
        {
            dl.AddRect(pos, pos + new Vector2(Lado, Lado), ImGui.GetColorU32(new Vector4(1f, 0.9f, 0.5f, 1f)), 4f, 0, 2f);
            ImGui.SetTooltip("FFBL: clique para abrir/fechar. Arraste para mover.");
        }
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4f))
        {
            arrastou = true;
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        }
        if (ImGui.IsItemDeactivated())
        {
            if (!arrastou) plugin.AlternarJanela();
            arrastou = false;
        }
    }
}
