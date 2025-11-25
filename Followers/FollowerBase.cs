using System.Numerics;

namespace FollowersPlugin.Followers;

public abstract class FollowerBase : IFollower
{
    protected bool isActive = false;
    protected Vector2 cursorPosition = Vector2.Zero;
    protected Vector2 targetPosition = Vector2.Zero;
    protected const float INTERPOLATION_SPEED = 0.15f;
    
    public abstract string Name { get; }
    public abstract bool CanRegenerate { get; }
    
    public bool IsActive => isActive;
    
    public virtual void Initialize()
    {
        isActive = true;
    }
    
    public virtual void Update(float deltaTime, Vector2 cursorPos)
    {
        cursorPosition = cursorPos;
    }
    
    public abstract void Draw();
    
    public virtual void Cleanup()
    {
        isActive = false;
    }
    
    public virtual void Regenerate()
    {
        if (CanRegenerate)
        {
            Cleanup();
            Initialize();
        }
    }
    
    protected float Lerp(float start, float end, float factor)
    {
        return start + (end - start) * factor;
    }
    
    protected Vector2 Lerp(Vector2 start, Vector2 end, float factor)
    {
        return start + (end - start) * factor;
    }
}

