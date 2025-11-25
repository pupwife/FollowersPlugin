using Dalamud.Configuration;
using System;

namespace FollowersPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsEnabled { get; set; } = false;
    public string? SelectedFollower { get; set; } = null;

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
