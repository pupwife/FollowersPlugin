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
    
    // Pastel color theme
    private static readonly Vector4 MintGreen = new(0.66f, 0.90f, 0.81f, 1.0f);      // #A8E6CF
    private static readonly Vector4 SoftPink = new(1.0f, 0.70f, 0.73f, 1.0f);        // #FFB3BA
    private static readonly Vector4 SoftPurple = new(0.78f, 0.81f, 0.92f, 1.0f);    // #C7CEEA
    private static readonly Vector4 LightOrange = new(1.0f, 0.83f, 0.65f, 1.0f);     // #FFD3A5
    private static readonly Vector4 PastelBackground = new(0.98f, 0.96f, 0.99f, 1.0f); // Very light pastel
    private static readonly Vector4 TextColor = new(0.3f, 0.3f, 0.35f, 1.0f);        // Soft dark gray

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

    public override void PreDraw()
    {
        // Apply pastel theme colors ONLY to this window using PushStyleColor
        // This ensures the theme doesn't affect other Dalamud plugins
        ImGui.PushStyleColor(ImGuiCol.WindowBg, PastelBackground);
        ImGui.PushStyleColor(ImGuiCol.Text, TextColor);
        ImGui.PushStyleColor(ImGuiCol.Button, SoftPurple);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(SoftPurple.X * 0.9f, SoftPurple.Y * 0.9f, SoftPurple.Z * 0.9f, SoftPurple.W));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(SoftPurple.X * 0.8f, SoftPurple.Y * 0.8f, SoftPurple.Z * 0.8f, SoftPurple.W));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(MintGreen.X, MintGreen.Y, MintGreen.Z, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(MintGreen.X, MintGreen.Y, MintGreen.Z, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(MintGreen.X, MintGreen.Y, MintGreen.Z, 0.7f));
        ImGui.PushStyleColor(ImGuiCol.Header, SoftPink);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(SoftPink.X * 0.9f, SoftPink.Y * 0.9f, SoftPink.Z * 0.9f, SoftPink.W));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(SoftPink.X * 0.8f, SoftPink.Y * 0.8f, SoftPink.Z * 0.8f, SoftPink.W));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, SoftPink);
    }
    
    public override void Draw()
    {
        ImGui.Text("Followers Plugin Configuration");
        
        // Pastel separator
        ImGui.PushStyleColor(ImGuiCol.Separator, LightOrange);
        ImGui.Separator();
        ImGui.PopStyleColor();
        ImGui.Spacing();

        // Enable/Disable toggle
        var isEnabled = configuration.IsEnabled;
        if (ImGui.Checkbox("Enable Followers", ref isEnabled))
        {
            configuration.IsEnabled = isEnabled;
            // When enabling, pass the selected follower from config to restore it
            plugin.FollowerManager.SetEnabled(isEnabled, configuration.SelectedFollower);
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
                ImGui.PushStyleColor(ImGuiCol.Button, LightOrange);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(LightOrange.X * 0.9f, LightOrange.Y * 0.9f, LightOrange.Z * 0.9f, LightOrange.W));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(LightOrange.X * 0.8f, LightOrange.Y * 0.8f, LightOrange.Z * 0.8f, LightOrange.W));
                if (ImGui.Button("Regenerate Follower"))
                {
                    plugin.FollowerManager.RegenerateCurrentFollower();
                }
                ImGui.PopStyleColor(3);
                ImGui.SameLine();
                ImGui.TextDisabled("(Procedural)");
            }
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Separator, LightOrange);
        ImGui.Separator();
        ImGui.PopStyleColor();
        
        ImGui.TextWrapped("Use /pfollowers to open this window.");
        ImGui.TextWrapped("Use /pfollowers regen to regenerate the current follower.");
    }
    
    public override void PostDraw()
    {
        // Pop all style colors to restore default theme for other windows
        ImGui.PopStyleColor(12); // Pop all the colors we pushed
    }
}
