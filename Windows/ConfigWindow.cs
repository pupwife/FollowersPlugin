using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FollowersPlugin.Followers;

namespace FollowersPlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private int selectedFollowerIndex = 0;

    public ConfigWindow(Plugin plugin) : base("Followers Configuration###FollowersConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 200);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        this.configuration = plugin.Configuration;
        
        // Set initial selected index
        var followerNames = plugin.FollowerManager.GetFollowerNames().ToList();
        var currentName = configuration.SelectedFollower;
        if (currentName != null && followerNames.Contains(currentName))
        {
            selectedFollowerIndex = followerNames.IndexOf(currentName);
        }
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Followers Plugin Configuration");
        ImGui.Separator();
        ImGui.Spacing();

        // Enable/Disable toggle
        var isEnabled = configuration.IsEnabled;
        if (ImGui.Checkbox("Enable Followers", ref isEnabled))
        {
            configuration.IsEnabled = isEnabled;
            plugin.FollowerManager.SetEnabled(isEnabled);
            configuration.Save();
        }

        ImGui.Spacing();

        // Follower selection dropdown
        var followerNames = plugin.FollowerManager.GetFollowerNames().ToList();
        if (followerNames.Count > 0)
        {
            var currentName = plugin.FollowerManager.GetCurrentFollowerName() ?? followerNames[0];
            var currentIndex = followerNames.IndexOf(currentName);
            if (currentIndex < 0) currentIndex = 0;
            
            if (ImGui.Combo("Follower Type", ref currentIndex, followerNames.ToArray(), followerNames.Count))
            {
                var selectedName = followerNames[currentIndex];
                configuration.SelectedFollower = selectedName;
                plugin.FollowerManager.SwitchFollower(selectedName);
                configuration.Save();
            }

            ImGui.Spacing();

            // Regenerate button (only show if current follower can regenerate)
            if (plugin.FollowerManager.CanRegenerateCurrentFollower())
            {
                if (ImGui.Button("Regenerate Follower"))
                {
                    plugin.FollowerManager.RegenerateCurrentFollower();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(Procedural)");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextWrapped("Use /pfollowers to open this window.");
        ImGui.TextWrapped("Use /pfollowers regen to regenerate the current follower.");
    }
}
