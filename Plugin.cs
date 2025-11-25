using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FollowersPlugin.Windows;
using FollowersPlugin.Followers;

namespace FollowersPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/pfollowers";

    public Configuration Configuration { get; init; }
    public FollowerManager FollowerManager { get; init; }

    public readonly WindowSystem WindowSystem = new("FollowersPlugin");
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        FollowerManager = new FollowerManager();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the followers configuration window. Use '/pfollowers regen' to regenerate the current follower."
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Initialize follower from config
        if (Configuration.IsEnabled && !string.IsNullOrEmpty(Configuration.SelectedFollower))
        {
            FollowerManager.SetEnabled(true);
            FollowerManager.SwitchFollower(Configuration.SelectedFollower);
        }

        Log.Information($"===Followers Plugin initialized: {PluginInterface.Manifest.Name}===");
    }

    private void Draw()
    {
        WindowSystem.Draw();
        
        // Update and draw followers (on top of everything)
        FollowerManager.Update();
        FollowerManager.Draw();
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        FollowerManager.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        args = args.Trim().ToLowerInvariant();
        
        if (args == "regen")
        {
            FollowerManager.RegenerateCurrentFollower();
            Log.Information("Regenerated current follower");
        }
        else
        {
            // Open config window
            ToggleConfigUi();
        }
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
