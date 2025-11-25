using System.Numerics;

namespace FollowersPlugin.Followers;

public interface IFollower
{
    string Name { get; }
    bool IsActive { get; }
    bool CanRegenerate { get; }
    
    void Initialize();
    void Update(float deltaTime, Vector2 cursorPos);
    void Draw();
    void Cleanup();
    void Regenerate();
}

