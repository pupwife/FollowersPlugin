using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace FollowersPlugin.Followers;

public class Fish2DFollower : FollowerBase
{
    public override string Name => "2D Fish";
    public override bool CanRegenerate => false;
    
    private const float CONSTRAINT = 25f;
    private new const float INTERPOLATION_SPEED = 0.15f;
    
    private struct ChainSegment
    {
        public Vector2 Position;
        public float Size;
    }
    
    private ChainSegment rootDot;
    private List<ChainSegment> chain = null!;
    private float curvature = 0f;
    
    public override void Initialize()
    {
        base.Initialize();
        
        var centerX = ImGui.GetMainViewport().Size.X / 2f;
        var centerY = ImGui.GetMainViewport().Size.Y / 2f;
        
        rootDot = new ChainSegment
        {
            Position = new Vector2(centerX, centerY),
            Size = 11f
        };
        
        targetPosition = rootDot.Position;
        
        // Initialize chain segments
        chain = new List<ChainSegment>
        {
            new ChainSegment { Position = new Vector2(centerX, centerY), Size = 11f },  // Head
            new ChainSegment { Position = new Vector2(centerX, centerY), Size = 6f },
            new ChainSegment { Position = new Vector2(centerX, centerY), Size = 19f },
            new ChainSegment { Position = new Vector2(centerX, centerY), Size = 16f },
            new ChainSegment { Position = new Vector2(centerX, centerY), Size = 12f },
            new ChainSegment { Position = new Vector2(centerX, centerY), Size = 6f },
            new ChainSegment { Position = new Vector2(centerX, centerY), Size = 3f },
        };
        
        // Initialize chain to starting position
        for (int i = 0; i < chain.Count; i++)
        {
            var segment = chain[i];
            segment.Position = new Vector2(centerX - i * CONSTRAINT, centerY);
            chain[i] = segment;
        }
    }
    
    public override void Update(float deltaTime, Vector2 cursorPos)
    {
        base.Update(deltaTime, cursorPos);
        
        // Smooth interpolation for root dot
        rootDot.Position = Lerp(rootDot.Position, cursorPos, INTERPOLATION_SPEED);
        
        if (chain.Count > 0)
        {
            chain[0] = rootDot;
        }
        
        // Update chain segments
        for (int i = 1; i < chain.Count; i++)
        {
            var prev = chain[i - 1];
            var current = chain[i];
            
            var dx = current.Position.X - prev.Position.X;
            var dy = current.Position.Y - prev.Position.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            
            if (dist > 0.01f)
            {
                var angle = MathF.Atan2(dy, dx);
                current.Position = new Vector2(
                    prev.Position.X + CONSTRAINT * MathF.Cos(angle),
                    prev.Position.Y + CONSTRAINT * MathF.Sin(angle)
                );
            }
            
            chain[i] = current;
        }
        
        // Calculate curvature
        CalculateCurvature();
    }
    
    private void CalculateCurvature()
    {
        float sum = 0f;
        for (int i = 0; i < chain.Count - 2; i++)
        {
            var p0 = chain[i].Position;
            var p1 = chain[i + 1].Position;
            var p2 = chain[i + 2].Position;
            
            var dx1 = p1.X - p0.X;
            var dy1 = p1.Y - p0.Y;
            var dx2 = p2.X - p1.X;
            var dy2 = p2.Y - p1.Y;
            
            var angle1 = MathF.Atan2(dy1, dx1);
            var angle2 = MathF.Atan2(dy2, dx2);
            var diff = angle2 - angle1;
            
            sum += diff;
        }
        curvature = sum;
    }
    
    public override void Draw()
    {
        if (!isActive || chain.Count == 0) return;
        
        var drawList = ImGui.GetForegroundDrawList();
        var viewport = ImGui.GetMainViewport();
        
        // Draw silhouette
        DrawSilhouette(drawList);
        
        // Draw tail
        DrawTail(drawList);
        
        // Draw fins
        DrawFin(drawList, 2, 9f);
        DrawFin(drawList, 4, 5f);
        
        // Draw eyes
        DrawEyes(drawList);
    }
    
    private void DrawSilhouette(ImDrawListPtr drawList)
    {
        if (chain.Count < 2) return;
        
        var points = new List<Vector2>();
        
        // Calculate silhouette points (matching JS logic exactly)
        var nextDot = chain[1].Position;
        var dx = nextDot.X - rootDot.Position.X;
        var dy = nextDot.Y - rootDot.Position.Y;
        var angle = MathF.Atan2(dy, dx) + MathF.PI;
        
        var silhouettePoint = new Vector2(
            rootDot.Position.X + rootDot.Size * MathF.Cos(angle),
            rootDot.Position.Y + rootDot.Size * MathF.Sin(angle)
        );
        points.Add(silhouettePoint);
        
        // Forward pass - matching JS logic exactly
        for (int i = 0; i < chain.Count; i++)
        {
            var haveNext = i + 1 < chain.Count;
            var referenceDot = haveNext ? chain[i + 1] : (i > 0 ? chain[i - 1] : chain[i]);
            var currentDot = chain[i];
            
            dx = referenceDot.Position.X - currentDot.Position.X;
            dy = referenceDot.Position.Y - currentDot.Position.Y;
            angle = MathF.Atan2(dy, dx) + (haveNext ? MathF.PI / 2f : -MathF.PI / 2f);
            
            silhouettePoint = new Vector2(
                currentDot.Position.X + currentDot.Size * MathF.Cos(angle),
                currentDot.Position.Y + currentDot.Size * MathF.Sin(angle)
            );
            points.Add(silhouettePoint);
        }
        
        // Backward pass - matching JS logic exactly
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var haveNext = i > 0;
            var referenceDot = haveNext ? chain[i - 1] : (i + 1 < chain.Count ? chain[i + 1] : chain[i]);
            var currentDot = chain[i];
            
            dx = referenceDot.Position.X - currentDot.Position.X;
            dy = referenceDot.Position.Y - currentDot.Position.Y;
            angle = MathF.Atan2(dy, dx) + (haveNext ? MathF.PI / 2f : -MathF.PI / 2f);
            
            silhouettePoint = new Vector2(
                currentDot.Position.X + currentDot.Size * MathF.Cos(angle),
                currentDot.Position.Y + currentDot.Size * MathF.Sin(angle)
            );
            points.Add(silhouettePoint);
        }
        
        // Add final point to close the shape
        if (points.Count > 0)
        {
            points.Add(points[0]);
        }
        
        // Draw with smooth curves using quadratic approximation
        if (points.Count >= 3)
        {
            var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0.37f, 0.39f, 0.43f, 1f)); // #5f656e
            var outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.82f));
            
            // Build smooth path using quadratic curve approximation
            var smoothPoints = new List<Vector2>();
            smoothPoints.Add(points[0]);
            
            for (int i = 1; i < points.Count - 1; i++) // Stop before last point (duplicate of first)
            {
                var nextPoint = i + 1 < points.Count ? points[i + 1] : points[0];
                
                // Approximate quadratic curve with intermediate points
                var control = points[i];
                var end = new Vector2((points[i].X + nextPoint.X) / 2f, (points[i].Y + nextPoint.Y) / 2f);
                var start = smoothPoints[smoothPoints.Count - 1];
                
                // Add curve points
                for (int j = 1; j <= 4; j++) // Start at 1 to avoid duplicating start point
                {
                    var t = j / 4f;
                    var curvePoint = new Vector2(
                        (1f - t) * (1f - t) * start.X + 2f * (1f - t) * t * control.X + t * t * end.X,
                        (1f - t) * (1f - t) * start.Y + 2f * (1f - t) * t * control.Y + t * t * end.Y
                    );
                    smoothPoints.Add(curvePoint);
                }
            }
            
            if (smoothPoints.Count >= 3)
            {
                var pointsArray = smoothPoints.ToArray();
                drawList.AddConvexPolyFilled(ref pointsArray[0], pointsArray.Length, color);
                drawList.AddPolyline(ref pointsArray[0], pointsArray.Length, outlineColor, ImDrawFlags.Closed, 1.5f);
            }
        }
    }
    
    private void DrawTail(ImDrawListPtr drawList)
    {
        if (chain.Count < 4) return;
        
        var points = new List<Vector2>();
        points.Add(chain[1].Position);
        
        // Create tail shape with curvature
        for (int i = 1; i < 4 && i < chain.Count; i++)
        {
            points.Add(chain[i].Position);
        }
        
        // Add curvature points
        for (int i = 3; i > 1 && i < chain.Count; i--)
        {
            if (i > 0)
            {
                var prev = chain[i - 1];
                var current = chain[i];
                
                var dx = prev.Position.X - current.Position.X;
                var dy = prev.Position.Y - current.Position.Y;
                var angle = MathF.Atan2(dy, dx) + curvature;
                
                var curvaturePoint = new Vector2(
                    current.Position.X + 5f * MathF.Cos(angle),
                    current.Position.Y + 5f * MathF.Sin(angle)
                );
                points.Add(curvaturePoint);
            }
        }
        
        if (points.Count >= 3)
        {
            var color = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.82f));
            var pointsArray = points.ToArray();
            drawList.AddConvexPolyFilled(ref pointsArray[0], pointsArray.Length, color);
        }
    }
    
    private void DrawFin(ImDrawListPtr drawList, int segmentIndex, float size)
    {
        if (segmentIndex >= chain.Count || segmentIndex < 2) return;
        
        var segment = chain[segmentIndex];
        var lastSegment = chain[segmentIndex - 2];
        
        // Calculate angle from lastSegment to segment (direction of body)
        var dx = segment.Position.X - lastSegment.Position.X;
        var dy = segment.Position.Y - lastSegment.Position.Y;
        var angle = MathF.Atan2(dy, dx);
        
        // Fins extend perpendicular to body direction (on the sides)
        var finSize = size * 3.5f;
        var fin1 = new Vector2(
            segment.Position.X + MathF.Cos(angle + MathF.PI / 2f) * finSize,
            segment.Position.Y + MathF.Sin(angle + MathF.PI / 2f) * finSize
        );
        var fin2 = new Vector2(
            segment.Position.X + MathF.Cos(angle - MathF.PI / 2f) * finSize,
            segment.Position.Y + MathF.Sin(angle - MathF.PI / 2f) * finSize
        );
        
        // Draw fin using quadratic curve approximation (matching JS)
        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.82f));
        
        // Create smooth fin shape with quadratic curves
        var finPoints = new List<Vector2>();
        finPoints.Add(segment.Position);
        
        // Curve to fin1
        for (int i = 0; i <= 4; i++)
        {
            var t = i / 4f;
            var midX = (segment.Position.X + fin1.X) / 2f;
            var midY = (segment.Position.Y + fin1.Y) / 2f;
            var x = (1f - t) * (1f - t) * segment.Position.X + 2f * (1f - t) * t * midX + t * t * fin1.X;
            var y = (1f - t) * (1f - t) * segment.Position.Y + 2f * (1f - t) * t * midY + t * t * fin1.Y;
            finPoints.Add(new Vector2(x, y));
        }
        
        // Curve to lastSegment
        for (int i = 1; i <= 4; i++)
        {
            var t = i / 4f;
            var midX = (fin1.X + lastSegment.Position.X) / 2f;
            var midY = (fin1.Y + lastSegment.Position.Y) / 2f;
            var x = (1f - t) * (1f - t) * fin1.X + 2f * (1f - t) * t * midX + t * t * lastSegment.Position.X;
            var y = (1f - t) * (1f - t) * fin1.Y + 2f * (1f - t) * t * midY + t * t * lastSegment.Position.Y;
            finPoints.Add(new Vector2(x, y));
        }
        
        // Curve to fin2
        for (int i = 1; i <= 4; i++)
        {
            var t = i / 4f;
            var midX = (lastSegment.Position.X + fin2.X) / 2f;
            var midY = (lastSegment.Position.Y + fin2.Y) / 2f;
            var x = (1f - t) * (1f - t) * lastSegment.Position.X + 2f * (1f - t) * t * midX + t * t * fin2.X;
            var y = (1f - t) * (1f - t) * lastSegment.Position.Y + 2f * (1f - t) * t * midY + t * t * fin2.Y;
            finPoints.Add(new Vector2(x, y));
        }
        
        // Curve back to segment
        for (int i = 1; i <= 4; i++)
        {
            var t = i / 4f;
            var midX = (fin2.X + segment.Position.X) / 2f;
            var midY = (fin2.Y + segment.Position.Y) / 2f;
            var x = (1f - t) * (1f - t) * fin2.X + 2f * (1f - t) * t * midX + t * t * segment.Position.X;
            var y = (1f - t) * (1f - t) * fin2.Y + 2f * (1f - t) * t * midY + t * t * segment.Position.Y;
            finPoints.Add(new Vector2(x, y));
        }
        
        if (finPoints.Count >= 3)
        {
            var pointsArray = finPoints.ToArray();
            drawList.AddConvexPolyFilled(ref pointsArray[0], pointsArray.Length, color);
        }
    }
    
    private void DrawEyes(ImDrawListPtr drawList)
    {
        if (chain.Count < 2) return;
        
        var head = chain[0];
        var next = chain[1];
        
        var dx = head.Position.X - next.Position.X;
        var dy = head.Position.Y - next.Position.Y;
        var angle = MathF.Atan2(dy, dx);
        
        const float spacing = 10f;
        const float eyeSize = 7.5f;
        const float pupilSize = 2.5f;
        
        var eye1 = new Vector2(
            head.Position.X + MathF.Cos(angle + MathF.PI / 2f) * spacing,
            head.Position.Y + MathF.Sin(angle + MathF.PI / 2f) * spacing
        );
        var eye2 = new Vector2(
            head.Position.X + MathF.Cos(angle - MathF.PI / 2f) * spacing,
            head.Position.Y + MathF.Sin(angle - MathF.PI / 2f) * spacing
        );
        
        var eyeColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.82f));
        var pupilColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f));
        
        drawList.AddCircleFilled(eye1, eyeSize, eyeColor);
        drawList.AddCircleFilled(eye2, eyeSize, eyeColor);
        drawList.AddCircleFilled(eye1, pupilSize, pupilColor);
        drawList.AddCircleFilled(eye2, pupilSize, pupilColor);
    }
}

