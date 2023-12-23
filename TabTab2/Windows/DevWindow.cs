using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Interface.Windowing;

namespace TabTab2.Windows;

public class DevWindow : Window
{
    public IList<string> PrintLines { get; set; } = new List<string>();
    public static int actorIdOffset { get => ActorIdOffset1; }
    private static int ActorIdOffset1 = 16;
    public DevWindow() : base("DevWindow")
    {

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 200),
            MaximumSize = Plugin.Resolution,
        };
    }

    public override void Draw()
    {
        foreach (var line in PrintLines)
        {
            ImGui.TextUnformatted(line);
        }
        PrintLines.Clear();

        ImGui.InputInt("Actor1ID", ref ActorIdOffset1);
    }

    public void Print(string text)
    {
        PrintLines.Add(text);
    }

    public void Separator()
    {
        PrintLines.Add("--------------------------");
    }
}
