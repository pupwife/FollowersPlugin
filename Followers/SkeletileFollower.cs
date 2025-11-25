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
        public float RelAngle;
        public float DefaultAngle;
        public float AbsAngle;
        public float Range;
        public float Stiffness;
        public bool IsSegment = true;
        
        public Segment(Segment parent, float size, float angle, float range, float stiffness)
        {
            Parent = parent;
            Size = size;
            RelAngle = angle;
            DefaultAngle = angle;
            Range = range;
            Stiffness = stiffness;
            
            if (parent != null)
            {
                AbsAngle = parent.AbsAngle + angle;
                Position = parent.Position;
                
                if (parent.Children != null)
                {
                    parent.Children.Add(this);
                }
                
                UpdateRelative(false, true);
            }
        }
        
        public void UpdateRelative(bool iter, bool flex)
        {
            if (Parent == null) return; // Root segment (Creature) doesn't update relative
            
            RelAngle = RelAngle - 2f * MathF.PI * MathF.Floor((RelAngle - DefaultAngle) / (2f * MathF.PI) + 0.5f);
            
            if (flex)
            {
                RelAngle = MathF.Min(
                    DefaultAngle + Range / 2f,
                    MathF.Max(
                        DefaultAngle - Range / 2f,
                        (RelAngle - DefaultAngle) / Stiffness + DefaultAngle
                    )
                );
            }
            
            AbsAngle = Parent.AbsAngle + RelAngle;
            Position = new Vector2(
                Parent.Position.X + MathF.Cos(AbsAngle) * Size,
                Parent.Position.Y + MathF.Sin(AbsAngle) * Size
            );
            
            if (iter)
            {
                foreach (var child in Children)
                {
                    child.UpdateRelative(iter, flex);
                }
            }
        }
        
        public void Follow(bool iter, bool flex)
        {
            if (Parent == null) return;
            
            var x = Parent.Position.X;
            var y = Parent.Position.Y;
            var dx = Position.X - x;
            var dy = Position.Y - y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            
            if (dist > 0.01f)
            {
                Position = new Vector2(
                    x + Size * dx / dist,
                    y + Size * dy / dist
                );
            }
            
            AbsAngle = MathF.Atan2(Position.Y - y, Position.X - x);
            RelAngle = AbsAngle - Parent.AbsAngle;
            UpdateRelative(false, flex);
            
            if (iter)
            {
                foreach (var child in Children)
                {
                    child.Follow(true, flex);
                }
            }
        }
    }
    
    private class LimbSystem
    {
        public Segment End;
        public int Length;
        public Creature Creature;
        public float Speed;
        public List<Segment> Nodes = new();
        public Segment Hip;
        
        public LimbSystem(Segment end, int length, float speed, Creature creature)
        {
            End = end;
            Length = Math.Max(1, length);
            Creature = creature;
            Speed = speed;
            creature.Systems.Add(this);
            
            var node = end;
            for (int i = 0; i < length; i++)
            {
                Nodes.Insert(0, node);
                node = node.Parent;
                if (node == null || !node.IsSegment)
                {
                    Length = i + 1;
                    break;
                }
            }
            Hip = Nodes[0].Parent!;
        }
        
        public virtual void MoveTo(float x, float y)
        {
            Nodes[0].UpdateRelative(true, true);
            var dx = x - End.Position.X;
            var dy = y - End.Position.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            var len = MathF.Max(0, dist - Speed);
            
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                var node = Nodes[i];
                var ang = MathF.Atan2(node.Position.Y - y, node.Position.X - x);
                node.Position = new Vector2(
                    x + len * MathF.Cos(ang),
                    y + len * MathF.Sin(ang)
                );
                x = node.Position.X;
                y = node.Position.Y;
                len = node.Size;
            }
            
            for (int i = 0; i < Nodes.Count; i++)
            {
                var node = Nodes[i];
                node.AbsAngle = MathF.Atan2(node.Position.Y - node.Parent!.Position.Y, node.Position.X - node.Parent.Position.X);
                node.RelAngle = node.AbsAngle - node.Parent.AbsAngle;
                
                foreach (var childNode in node.Children)
                {
                    if (!Nodes.Contains(childNode))
                    {
                        childNode.UpdateRelative(true, false);
                    }
                }
            }
        }
        
        public virtual void Update(float x, float y)
        {
            MoveTo(x, y);
        }
    }
    
    private class LegSystem : LimbSystem
    {
        public float GoalX;
        public float GoalY;
        public int Step;
        public float Forwardness;
        public float Reach;
        public float Swing;
        public float SwingOffset;
        private Random random;
        
        public LegSystem(Segment end, int length, float speed, Creature creature, Random random) 
            : base(end, length, speed, creature)
        {
            this.random = random;
            GoalX = end.Position.X;
            GoalY = end.Position.Y;
            Step = 0;
            Forwardness = 0;
            
            var dx = End.Position.X - Hip.Position.X;
            var dy = End.Position.Y - Hip.Position.Y;
            Reach = 0.9f * MathF.Sqrt(dx * dx + dy * dy);
            
            var relAngle = creature.AbsAngle - MathF.Atan2(End.Position.Y - Hip.Position.Y, End.Position.X - Hip.Position.X);
            relAngle -= 2f * MathF.PI * MathF.Floor(relAngle / (2f * MathF.PI) + 0.5f);
            Swing = -relAngle + (2f * (relAngle < 0 ? 1 : 0) - 1) * MathF.PI / 2f;
            SwingOffset = creature.AbsAngle - Hip.AbsAngle;
        }
        
        public override void Update(float x, float y)
        {
            MoveTo(GoalX, GoalY);
            
            if (Step == 0)
            {
                var dx = End.Position.X - GoalX;
                var dy = End.Position.Y - GoalY;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                
                if (dist > 1f)
                {
                    Step = 1;
                    var randomOffset = (float)(random.NextDouble() * 2.0 - 1.0) * Reach / 2f;
                    GoalX = Hip.Position.X + Reach * MathF.Cos(Swing + Hip.AbsAngle + SwingOffset) + randomOffset;
                    GoalY = Hip.Position.Y + Reach * MathF.Sin(Swing + Hip.AbsAngle + SwingOffset) + randomOffset;
                }
            }
            else if (Step == 1)
            {
                var theta = MathF.Atan2(End.Position.Y - Hip.Position.Y, End.Position.X - Hip.Position.X) - Hip.AbsAngle;
                var dx = End.Position.X - Hip.Position.X;
                var dy = End.Position.Y - Hip.Position.Y;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                var forwardness2 = dist * MathF.Cos(theta);
                var dF = Forwardness - forwardness2;
                Forwardness = forwardness2;
                
                if (dF * dF < 1f)
                {
                    Step = 0;
                    GoalX = Hip.Position.X + (End.Position.X - Hip.Position.X);
                    GoalY = Hip.Position.Y + (End.Position.Y - Hip.Position.Y);
                }
            }
        }
    }
    
    private class Creature : Segment
    {
        public float FSpeed;
        public float FAccel;
        public float FFric;
        public float FRes;
        public float FThresh;
        public float RSpeed;
        public float RAccel;
        public float RFric;
        public float RRes;
        public float RThresh;
        public float Speed;
        public List<LimbSystem> Systems = new();
        
        public Creature(float x, float y, float angle, float fAccel, float fFric, float fRes, float fThresh,
            float rAccel, float rFric, float rRes, float rThresh) : base(null!, 0, 0, 0, 0)
        {
            Position = new Vector2(x, y);
            AbsAngle = angle;
            FSpeed = 0;
            FAccel = fAccel;
            FFric = fFric;
            FRes = fRes;
            FThresh = fThresh;
            RSpeed = 0;
            RAccel = rAccel;
            RFric = rFric;
            RRes = rRes;
            RThresh = rThresh;
            Speed = 0;
            Parent = null;
            IsSegment = false;
        }
        
        public void Follow(float x, float y, float deltaTime)
        {
            // Simplified follow behavior - move towards cursor
            var dx = x - Position.X;
            var dy = y - Position.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            var targetAngle = MathF.Atan2(y - Position.Y, x - Position.X);
            
            // Update forward movement
            var accel = FAccel;
            if (Systems.Count > 0)
            {
                var sum = 0;
                foreach (var system in Systems)
                {
                    if (system is LegSystem legSystem && legSystem.Step == 0)
                    {
                        sum++;
                    }
                }
                accel *= sum / (float)Systems.Count;
            }
            
            FSpeed += accel * (dist > FThresh ? 1 : 0);
            FSpeed *= 1f - FRes;
            Speed = MathF.Max(0, FSpeed - FFric);
            
            // Update rotation
            var dif = AbsAngle - targetAngle;
            dif -= 2f * MathF.PI * MathF.Floor(dif / (2f * MathF.PI) + 0.5f);
            
            if (MathF.Abs(dif) > RThresh && dist > FThresh)
            {
                RSpeed -= RAccel * (dif > 0 ? 1 : -1);
            }
            RSpeed *= 1f - RRes;
            if (MathF.Abs(RSpeed) > RFric)
            {
                RSpeed -= RFric * (RSpeed > 0 ? 1 : -1);
            }
            else
            {
                RSpeed = 0;
            }
            
            // Update position
            AbsAngle += RSpeed;
            AbsAngle -= 2f * MathF.PI * MathF.Floor(AbsAngle / (2f * MathF.PI) + 0.5f);
            Position = new Vector2(
                Position.X + Speed * MathF.Cos(AbsAngle),
                Position.Y + Speed * MathF.Sin(AbsAngle)
            );
            
            AbsAngle += MathF.PI;
            foreach (var child in Children)
            {
                child.Follow(true, true);
            }
            foreach (var system in Systems)
            {
                system.Update(x, y);
            }
            AbsAngle -= MathF.PI;
        }
    }
    
    private Creature? critter;
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
        // Leg count should be even and scale with length
        var tailLength = random.Next(4, 20);
        // More legs for longer creatures: base 2 legs, add 2 per ~3 tail segments
        // This gives: 4 tail = 2 legs, 7 tail = 4 legs, 10 tail = 6 legs, 13 tail = 8 legs, 16 tail = 10 legs, 19 tail = 12 legs
        legCount = 2 + (tailLength / 3) * 2;
        // Ensure even number
        if (legCount % 2 != 0) legCount++;
        // Cap at reasonable max (12 legs = 6 pairs)
        legCount = Math.Min(legCount, 12);
        // Ensure minimum of 2 legs
        legCount = Math.Max(legCount, 2);
        
        creatureSize = 0.8f + (float)random.NextDouble() * 0.4f;
        var baseSize = 8f / MathF.Sqrt(legCount) * 0.2f * creatureSize;
        
        // Calculate speed multiplier based on leg count
        var speedMultiplier = 0.5f + (legCount - 1) * (2.5f / 11f);
        
        SetupLizard(baseSize, legCount, tailLength, speedMultiplier);
    }
    
    private void SetupLizard(float size, int legs, int tail, float speedMultiplier)
    {
        var s = size;
        var frictionMultiplier = 1.5f - (speedMultiplier * 0.25f);
        
        critter = new Creature(
            ImGui.GetMainViewport().Size.X / 2f,
            ImGui.GetMainViewport().Size.Y / 2f,
            0,
            s * 10f * speedMultiplier,
            s * 2f * frictionMultiplier,
            0.5f,
            16f,
            0.5f * speedMultiplier,
            0.085f,
            0.5f,
            0.3f
        );
        
        Segment spinal = critter;
        
        // Neck
        for (int i = 0; i < 6; i++)
        {
            spinal = new Segment(spinal, s * 4f, 0, MathF.PI * 2f / 3f, 1.1f);
            for (int j = -1; j <= 1; j += 2)
            {
                var node = new Segment(spinal, s * 3f, j, 0.1f, 2f);
                for (int k = 0; k < 3; k++)
                {
                    node = new Segment(node, s * 0.1f, -j * 0.1f, 0.1f, 2f);
                }
            }
        }
        
        // Torso and legs
        for (int i = 0; i < legs; i++)
        {
            if (i > 0)
            {
                // Vertebrae and ribs
                for (int j = 0; j < 6; j++)
                {
                    spinal = new Segment(spinal, s * 4f, 0, MathF.PI / 2f, 1.5f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var node = new Segment(spinal, s * 3f, k * MathF.PI / 2f, 0.1f, 1.5f);
                        for (int l = 0; l < 3; l++)
                        {
                            node = new Segment(node, s * 3f, -k * 0.3f, 0.1f, 2f);
                        }
                    }
                }
            }
            
            // Legs and shoulders (half on each side)
            for (int j = -1; j <= 1; j += 2)
            {
                var node = new Segment(spinal, s * 12f, j * MathF.PI / 4f, 0, 8f);
                node = new Segment(node, s * 16f, -j * MathF.PI / 4f, MathF.PI * 2f, 1f);
                node = new Segment(node, s * 16f, j * MathF.PI / 2f, MathF.PI, 2f);
                for (int k = 0; k < 4; k++)
                {
                    new Segment(node, s * 4f, (k / 3f - 0.5f) * MathF.PI / 2f, 0.1f, 4f);
                }
                _ = new LegSystem(node, 3, s * 12f * speedMultiplier, critter, random);
            }
        }
        
        // Tail
        for (int i = 0; i < tail; i++)
        {
            spinal = new Segment(spinal, s * 4f, 0, MathF.PI * 2f / 3f, 1.1f);
            for (int j = -1; j <= 1; j += 2)
            {
                var node = new Segment(spinal, s * 3f, j, 0.1f, 2f);
                for (int k = 0; k < 3; k++)
                {
                    new Segment(node, s * 3f * (tail - i) / tail, -j * 0.1f, 0.1f, 2f);
                }
            }
        }
    }
    
    public override void Update(float deltaTime, Vector2 cursorPos)
    {
        base.Update(deltaTime, cursorPos);
        
        if (critter == null) return;
        
        // Update creature with physics
        critter.Follow(cursorPos.X, cursorPos.Y, deltaTime);
    }
    
    public override void Draw()
    {
        if (!isActive || critter == null) return;
        
        var drawList = ImGui.GetForegroundDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0.91f, 0.91f, 0.85f, 1f)); // Bone color
        
        DrawSegment(drawList, critter, color);
    }
    
    private void DrawSegment(ImDrawListPtr drawList, Segment segment, uint color)
    {
        if (segment.Parent != null)
        {
            drawList.AddLine(segment.Parent.Position, segment.Position, color, 1f);
        }
        
        // Draw joint - matching JS: partial arc (3/4 circle) + triangle
        var r = 2f;
        var absAngle = segment.Parent != null 
            ? MathF.Atan2(segment.Position.Y - segment.Parent.Position.Y, segment.Position.X - segment.Parent.Position.X)
            : segment.AbsAngle;
        
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
            drawList.AddLine(arcPoints[i], arcPoints[i + 1], color, 1f);
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
        
        drawList.AddLine(endPoint, centerPoint, color, 1f);
        drawList.AddLine(centerPoint, startPoint, color, 1f);
        
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
        critter = null;
    }
}
