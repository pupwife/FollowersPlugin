using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FollowersPlugin.Followers;

public class SkeletileFollower : FollowerBase
{
    public override string Name => "Skeletile";
    public override bool CanRegenerate => true;
    
    private class Segment
    {
        public Vector2 Position;
        public Segment? Parent;
        public List<Segment> Children = new();
        public float Size;
        public float Angle;
        public float DefaultAngle;
    }
    
    private Segment? root;
    private List<Segment> allSegments = null!;
    private int legCount;
    private float creatureSize;
    private Random random = null!;
    
    public override void Initialize()
    {
        base.Initialize();
        random = new Random();
        
        var viewport = ImGui.GetMainViewport();
        var centerX = viewport.Size.X / 2f;
        var centerY = viewport.Size.Y / 2f;
        
        // Generate random creature parameters
        legCount = random.Next(1, 13);
        creatureSize = 0.8f + (float)random.NextDouble() * 0.4f;
        var baseSize = 8f / MathF.Sqrt(legCount) * 0.2f * creatureSize;
        var tailLength = random.Next(4, legCount * 8 + 4);
        
        allSegments = new List<Segment>();
        root = new Segment
        {
            Position = new Vector2(centerX, centerY),
            Size = baseSize,
            Angle = 0f,
            DefaultAngle = 0f
        };
        allSegments.Add(root);
        
        BuildCreature(root, baseSize, legCount, tailLength);
    }
    
    private void BuildCreature(Segment parent, float size, int legs, int tailLength)
    {
        // Build neck
        var current = parent;
        for (int i = 0; i < 6; i++)
        {
            current = CreateSegment(current, size * 4f, 0f, MathF.PI * 2f / 3f);
            for (int j = -1; j <= 1; j += 2)
            {
                var node = CreateSegment(current, size * 3f, j, 0.1f);
                for (int k = 0; k < 3; k++)
                {
                    node = CreateSegment(node, size * 0.1f, -j * 0.1f, 0.1f);
                }
            }
        }
        
        // Build torso and legs
        for (int i = 0; i < legs; i++)
        {
            if (i > 0)
            {
                for (int j = 0; j < 6; j++)
                {
                    current = CreateSegment(current, size * 4f, 0f, MathF.PI / 2f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var node = CreateSegment(current, size * 3f, k * MathF.PI / 2f, 0.1f);
                        for (int l = 0; l < 3; l++)
                        {
                            node = CreateSegment(node, size * 3f, -k * 0.3f, 0.1f);
                        }
                    }
                }
            }
            
            // Legs
            for (int j = -1; j <= 1; j += 2)
            {
                var node = CreateSegment(current, size * 12f, j * MathF.PI / 4f, 0f);
                node = CreateSegment(node, size * 16f, -j * MathF.PI / 4f, MathF.PI * 2f);
                node = CreateSegment(node, size * 16f, j * MathF.PI / 2f, MathF.PI);
                for (int k = 0; k < 4; k++)
                {
                    CreateSegment(node, size * 4f, (k / 3f - 0.5f) * MathF.PI / 2f, 0.1f);
                }
            }
        }
        
        // Build tail
        for (int i = 0; i < tailLength; i++)
        {
            current = CreateSegment(current, size * 4f, 0f, MathF.PI * 2f / 3f);
            for (int j = -1; j <= 1; j += 2)
            {
                var node = CreateSegment(current, size * 3f, j, 0.1f);
                for (int k = 0; k < 3; k++)
                {
                    CreateSegment(node, size * 3f * (tailLength - i) / tailLength, -j * 0.1f, 0.1f);
                }
            }
        }
    }
    
    private Segment CreateSegment(Segment parent, float size, float angle, float range)
    {
        var segment = new Segment
        {
            Parent = parent,
            Size = size,
            Angle = angle,
            DefaultAngle = angle,
            Position = parent.Position
        };
        parent.Children.Add(segment);
        allSegments.Add(segment);
        return segment;
    }
    
    public override void Update(float deltaTime, Vector2 cursorPos)
    {
        base.Update(deltaTime, cursorPos);
        
        if (root == null) return;
        
        // Update root position to follow cursor with smooth interpolation
        var dist = Vector2.Distance(root.Position, cursorPos);
        root.Position = Lerp(root.Position, cursorPos, 0.1f);
        
        // Update all segments
        UpdateSegment(root);
    }
    
    private void UpdateSegment(Segment segment)
    {
        if (segment.Parent != null)
        {
            var dx = segment.Position.X - segment.Parent.Position.X;
            var dy = segment.Position.Y - segment.Parent.Position.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            
            if (dist > 0.01f)
            {
                segment.Position = new Vector2(
                    segment.Parent.Position.X + segment.Size * dx / dist,
                    segment.Parent.Position.Y + segment.Size * dy / dist
                );
            }
        }
        
        foreach (var child in segment.Children)
        {
            UpdateSegment(child);
        }
    }
    
    public override void Draw()
    {
        if (!isActive || root == null) return;
        
        var drawList = ImGui.GetForegroundDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0.91f, 0.91f, 0.85f, 1f)); // Bone color
        
        DrawSegment(drawList, root, color);
    }
    
    private void DrawSegment(ImDrawListPtr drawList, Segment segment, uint color)
    {
        if (segment.Parent != null)
        {
            drawList.AddLine(segment.Parent.Position, segment.Position, color, 2f);
        }
        
        // Draw joint - matching JS: partial arc (3/4 circle) + triangle
        var r = 4f;
        var absAngle = segment.Parent != null 
            ? MathF.Atan2(segment.Position.Y - segment.Parent.Position.Y, segment.Position.X - segment.Parent.Position.X)
            : 0f;
        
        // Draw partial arc from PI/4 to 7*PI/4 (3/4 circle)
        var startAngle = MathF.PI / 4f + absAngle;
        var endAngle = 7f * MathF.PI / 4f + absAngle;
        
        // Draw arc using multiple line segments
        var arcPoints = new List<Vector2>();
        const int arcSegments = 24; // 3/4 of 32 segments
        for (int i = 0; i <= arcSegments; i++)
        {
            var t = i / (float)arcSegments;
            var angle = startAngle + t * (endAngle - startAngle);
            arcPoints.Add(new Vector2(
                segment.Position.X + r * MathF.Cos(angle),
                segment.Position.Y + r * MathF.Sin(angle)
            ));
        }
        
        // Draw arc outline
        for (int i = 0; i < arcPoints.Count - 1; i++)
        {
            drawList.AddLine(arcPoints[i], arcPoints[i + 1], color, 2f);
        }
        
        // Draw triangle pointing in direction of movement
        var endPoint = new Vector2(
            segment.Position.X + r * MathF.Cos(7f * MathF.PI / 4f + absAngle),
            segment.Position.Y + r * MathF.Sin(7f * MathF.PI / 4f + absAngle)
        );
        var centerPoint = new Vector2(
            segment.Position.X + r * MathF.Cos(absAngle) * MathF.Sqrt(2f),
            segment.Position.Y + r * MathF.Sin(absAngle) * MathF.Sqrt(2f)
        );
        var startPoint = new Vector2(
            segment.Position.X + r * MathF.Cos(MathF.PI / 4f + absAngle),
            segment.Position.Y + r * MathF.Sin(MathF.PI / 4f + absAngle)
        );
        
        drawList.AddLine(endPoint, centerPoint, color, 2f);
        drawList.AddLine(centerPoint, startPoint, color, 2f);
        
        foreach (var child in segment.Children)
        {
            DrawSegment(drawList, child, color);
        }
    }
    
    public override void Regenerate()
    {
        Cleanup();
        Initialize();
    }
    
    public override void Cleanup()
    {
        base.Cleanup();
            root = null!;
        allSegments?.Clear();
    }
}

