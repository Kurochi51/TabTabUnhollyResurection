using System.Numerics;

using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;

namespace TabTab2.Windows;

public class ConfigWindow : Window
{
    private readonly DalamudPluginInterface pluginInterface;
    private readonly Configuration config;

    public ConfigWindow(Configuration _config, DalamudPluginInterface _pluginInterface) : base("TabTab Settings")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 250),
            MaximumSize = Plugin.Resolution,
        };
        config = _config;
        pluginInterface = _pluginInterface;
    }

    public override void OnClose()
    {
        config.Save(pluginInterface);
    }

    public override void Draw()
    {
        var enabled = config.Enabled;
        if (ImGui.Checkbox("Enable", ref enabled))
        {
            config.Enabled = enabled;
        }
        if (config.Enabled)
        {
            ImGui.TextUnformatted("Your in-game tab targeting settings will be ignored while\nTabTab is enabled, and settings here will be used instead.");
        }

        ImGui.Separator();
        ImGui.Text("Targeting Mode");
        var targetMode = config.TargettingMode;
        if (ImGui.RadioButton("Target HP (Highest to Lowest)", ref targetMode, 1))
        {
            config.TargettingMode = targetMode;
        }
        if (ImGui.RadioButton("Target HP (Lowest to Highest)", ref targetMode, 2))
        {
            config.TargettingMode = targetMode;
        }
    }
}
