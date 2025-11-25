using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FollowersPlugin.Followers;

public class DragonFollower : FollowerBase
{
    public override string Name => "Dragon";
    public override bool CanRegenerate => true;
    
    private const int MIN_SEGMENTS = 15;
    private const int MAX_SEGMENTS = 35;
    
    private struct DragonParams
    {
        public int SegmentCount;
        public List<int> FinPositions;
        public Vector4 HeadColor;
        public Vector4 FinGradientStart;
        public Vector4 FinGradientEnd;
        public Vector4 SpineGradientStart;
        public Vector4 SpineGradientEnd;
        public float Scale;
        public float SegmentSpacing;
        public float AnimationSpeed;
    }
    
    private DragonParams dragonParams;
    private List<Vector2> segments;
    private float rad = 0f;
    private float frm;
    private Random random;
    
    public override void Initialize()
    {
        base.Initialize();
        random = new Random();
        frm = (float)random.NextDouble();
        GenerateDragonParams();
        InitializeSegments();
    }
    
    private void GenerateDragonParams()
    {
        var segmentCount = random.Next(MIN_SEGMENTS, MAX_SEGMENTS + 1);
        var finCount = random.Next(2, 5);
        var finPositions = new List<int>();
        
        for (int i = 0; i < finCount; i++)
        {
            int pos;
            do
            {
                pos = random.Next(2, segmentCount - 2);
            } while (finPositions.Contains(pos));
            finPositions.Add(pos);
        }
        finPositions.Sort();
        
        var headColors = new[]
        {
            new Vector4(1f, 1f, 1f, 1f),      // white
            new Vector4(1f, 0.8f, 0.8f, 1f),  // light red
            new Vector4(0.8f, 1f, 0.8f, 1f),  // light green
            new Vector4(0.8f, 0.8f, 1f, 1f),  // light blue
            new Vector4(1f, 1f, 0.8f, 1f),    // light yellow
            new Vector4(1f, 0.8f, 1f, 1f)     // light magenta
        };
        
        var headColor = headColors[random.Next(headColors.Length)];
        
        var gradients = new[]
        {
            (new Vector4(0.8f, 0.8f, 0.8f, 1f), new Vector4(0f, 0f, 0f, 1f)),
            (new Vector4(0.87f, 0.87f, 0.87f, 1f), new Vector4(0.2f, 0.2f, 0.2f, 1f)),
            (new Vector4(0.73f, 0.73f, 0.73f, 1f), new Vector4(0.13f, 0.13f, 0.13f, 1f)),
        };
        
        var finGradient = gradients[random.Next(gradients.Length)];
        var spineGradient = gradients[random.Next(gradients.Length)];
        
        dragonParams = new DragonParams
        {
            SegmentCount = segmentCount,
            FinPositions = finPositions,
            HeadColor = headColor,
            FinGradientStart = finGradient.Item1,
            FinGradientEnd = finGradient.Item2,
            SpineGradientStart = spineGradient.Item1,
            SpineGradientEnd = spineGradient.Item2,
            Scale = 0.15f + (float)random.NextDouble() * 0.3f,
            SegmentSpacing = 4f + (float)random.NextDouble() * 2f,
            AnimationSpeed = 0.002f + (float)random.NextDouble() * 0.002f
        };
    }
    
    private void InitializeSegments()
    {
        segments = new List<Vector2>();
        var viewport = ImGui.GetMainViewport();
        var centerX = viewport.Size.X / 2f;
        var centerY = viewport.Size.Y / 2f;
        
        for (int i = 0; i < dragonParams.SegmentCount; i++)
        {
            segments.Add(new Vector2(centerX, centerY));
        }
        
        targetPosition = new Vector2(centerX, centerY);
    }
    
    public override void Update(float deltaTime, Vector2 cursorPos)
    {
        base.Update(deltaTime, cursorPos);
        
        if (segments == null || segments.Count == 0) return;
        
        var viewport = ImGui.GetMainViewport();
        var width = viewport.Size.X;
        var height = viewport.Size.Y;
        
        // Update first segment (head) to follow cursor with smooth interpolation
        var head = segments[0];
        var ax = MathF.Cos(3f * frm) * rad * width / height;
        var ay = MathF.Sin(4f * frm) * rad * height / width;
        
        head.X += (ax + cursorPos.X - head.X) / 10f;
        head.Y += (ay + cursorPos.Y - head.Y) / 10f;
        segments[0] = head;
        
        // Update other segments
        for (int i = 1; i < segments.Count; i++)
        {
            var current = segments[i];
            var prev = segments[i - 1];
            
            var a = MathF.Atan2(current.Y - prev.Y, current.X - prev.X);
            current.X += (prev.X - current.X + MathF.Cos(a) * (100f - i) / dragonParams.SegmentSpacing) / 4f;
            current.Y += (prev.Y - current.Y + MathF.Sin(a) * (100f - i) / dragonParams.SegmentSpacing) / 4f;
            
            segments[i] = current;
        }
        
        // Update animation
        if (rad < MathF.Min(cursorPos.X, cursorPos.Y) - 20f)
        {
            rad++;
        }
        frm += dragonParams.AnimationSpeed;
        
        // Return to center if idle
        if (rad > 60f)
        {
            cursorPosition.X += (width / 2f - cursorPosition.X) * 0.05f;
            cursorPosition.Y += (height / 2f - cursorPosition.Y) * 0.05f;
        }
    }
    
    public override void Draw()
    {
        if (!isActive || segments == null || segments.Count == 0) return;
        
        var drawList = ImGui.GetForegroundDrawList();
        
        // Draw segments
        for (int i = 1; i < segments.Count; i++)
        {
            var prev = segments[i - 1];
            var current = segments[i];
            
            var a = MathF.Atan2(current.Y - prev.Y, current.X - prev.X);
            var center = new Vector2((prev.X + current.X) / 2f, (prev.Y + current.Y) / 2f);
            var s = ((162f + 4f * (1f - i)) / 50f) * dragonParams.Scale;
            
            // Determine segment type
            bool isHead = i == 1;
            bool isFin = dragonParams.FinPositions.Contains(i);
            
            Vector4 color;
            float size;
            
            if (isHead)
            {
                color = dragonParams.HeadColor;
                size = 20f * s;
            }
            else if (isFin)
            {
                // Interpolate fin gradient
                var t = (float)(i - 1) / (segments.Count - 2);
                color = Lerp(dragonParams.FinGradientStart, dragonParams.FinGradientEnd, t);
                size = 15f * s;
            }
            else
            {
                // Interpolate spine gradient
                var t = (float)(i - 1) / (segments.Count - 2);
                color = Lerp(dragonParams.SpineGradientStart, dragonParams.SpineGradientEnd, t);
                size = 8f * s;
            }
            
            var drawColor = ImGui.ColorConvertFloat4ToU32(color);
            
            // Draw segment as rotated ellipse
            var angleDeg = a * 180f / MathF.PI;
            DrawRotatedEllipse(drawList, center, new Vector2(size, size * 0.5f), angleDeg, drawColor);
        }
    }
    
    private void DrawRotatedEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 size, float angleDeg, uint color)
    {
        const int segments = 16;
        var points = new Vector2[segments];
        
        for (int i = 0; i < segments; i++)
        {
            var angle = i * 2f * MathF.PI / segments;
            var x = size.X * MathF.Cos(angle);
            var y = size.Y * MathF.Sin(angle);
            
            // Rotate
            var cos = MathF.Cos(angleDeg * MathF.PI / 180f);
            var sin = MathF.Sin(angleDeg * MathF.PI / 180f);
            var rotX = x * cos - y * sin;
            var rotY = x * sin + y * cos;
            
            points[i] = new Vector2(center.X + rotX, center.Y + rotY);
        }
        
        drawList.AddConvexPolyFilled(ref points[0], segments, color);
    }
    
    public override void Regenerate()
    {
        Cleanup();
        Initialize();
    }
    
    private Vector4 Lerp(Vector4 start, Vector4 end, float t)
    {
        return new Vector4(
            start.X + (end.X - start.X) * t,
            start.Y + (end.Y - start.Y) * t,
            start.Z + (end.Z - start.Z) * t,
            start.W + (end.W - start.W) * t
        );
    }
}

