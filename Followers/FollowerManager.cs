using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FollowersPlugin.Followers;

public class FollowerManager : IDisposable
{
    private readonly Dictionary<string, IFollower> followers = new();
    private IFollower? currentFollower;
    private string? currentFollowerName;
    private bool isEnabled = false;
    private DateTime lastFrameTime = DateTime.Now;
    private Vector2 lastCursorPosition = Vector2.Zero;
    
    public FollowerManager()
    {
        // Register all followers
        RegisterFollower(new Fish2DFollower());
        RegisterFollower(new DragonFollower());
        RegisterFollower(new SkeletileFollower());
    }
    
    private void RegisterFollower(IFollower follower)
    {
        followers[follower.Name] = follower;
    }
    
    public IEnumerable<string> GetFollowerNames()
    {
        return followers.Keys;
    }
    
    public string? GetCurrentFollowerName()
    {
        return currentFollowerName;
    }
    
    public bool IsEnabled => isEnabled;
    
    public void SetEnabled(bool enabled, string? preferredFollowerName = null)
    {
        isEnabled = enabled;
        if (!enabled)
        {
            // When disabling, preserve the current follower name so we can restore it later
            // Don't clear currentFollowerName - just cleanup the follower instance
            if (currentFollower != null)
            {
                currentFollower.Cleanup();
                currentFollower = null;
            }
        }
        else
        {
            // When enabling, restore the preferred follower or current follower name
            var followerToRestore = preferredFollowerName ?? currentFollowerName;
            if (followerToRestore != null && followers.ContainsKey(followerToRestore))
            {
                SwitchFollower(followerToRestore);
            }
            else if (currentFollowerName == null && followers.Count > 0)
            {
                // Default to first follower only if no preference and no current follower
                SwitchFollower(followers.Keys.First());
            }
        }
    }
    
    public void SwitchFollower(string? followerName)
    {
        // Cleanup current follower
        if (currentFollower != null)
        {
            currentFollower.Cleanup();
            currentFollower = null;
        }
        
        currentFollowerName = followerName;
        
        // Initialize new follower
        if (followerName != null && followers.TryGetValue(followerName, out var follower))
        {
            follower.Initialize();
            currentFollower = follower;
        }
    }
    
    public void RegenerateCurrentFollower()
    {
        if (currentFollower != null && currentFollower.CanRegenerate)
        {
            currentFollower.Regenerate();
        }
    }
    
    public bool CanRegenerateCurrentFollower()
    {
        return currentFollower != null && currentFollower.CanRegenerate;
    }
    
    public void Update()
    {
        if (!isEnabled || currentFollower == null) return;
        
        // Calculate delta time
        var currentTime = DateTime.Now;
        var deltaTime = (float)(currentTime - lastFrameTime).TotalSeconds;
        if (deltaTime < 0 || deltaTime > 0.1f) deltaTime = 0.016f; // Cap at ~60fps
        lastFrameTime = currentTime;
        
        // Get cursor position from ImGui (screen coordinates)
        var cursorPos = ImGui.GetMousePos();
        lastCursorPosition = cursorPos;
        
        // Update follower
        currentFollower.Update(deltaTime, cursorPos);
    }
    
    public void Draw()
    {
        if (!isEnabled || currentFollower == null) return;
        
        currentFollower.Draw();
    }
    
    public void Dispose()
    {
        foreach (var follower in followers.Values)
        {
            if (follower.IsActive)
            {
                follower.Cleanup();
            }
        }
        followers.Clear();
        currentFollower = null;
    }
}

