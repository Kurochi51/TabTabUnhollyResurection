using Dalamud.Configuration;
using Dalamud.Plugin;

namespace TabTab2;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool Enabled { get; set; } = false;
    public int TargettingMode { get; set; } = 1;

    public void Save(DalamudPluginInterface pi) => pi.SavePluginConfig(this);
}
