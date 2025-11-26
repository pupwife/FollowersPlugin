using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FollowersPlugin.Followers;

public class SkeletileFollower : FollowerBase
{
    public override string Name => "Skeletile";
    public override bool CanRegenerate => true;
    
    // ========== HUNTER/STALKER PERSONALITY CONSTANTS ==========
    private const float IDLE_THRESHOLD = 1200f; // ms before cursor is considered idle
    private const float IDLE_RADIUS = 25f; // Pixels - cursor must stay within this radius to be "idle"
    private const float COIL_RADIUS = IDLE_RADIUS; // Match the idle radius for coiling
    private const float STALK_DISTANCE = 250f; // How far behind to lag (stalking distance)
    private const float POUNCE_TRIGGER = 2500f; // ms of being idle before pouncing
    private const float LAG_SPEED = 0.1f; // How fast stalk position catches up (0-1, lower = more lag)
    private const float AGGRESSION_INCREASE_RATE = 0.0005f; // How fast aggression builds (per ms)
    private const int HISTORY_SIZE = 10;
    
    private class CursorHistoryEntry
    {
        public Vector2 Position;
        public float Time;
    }
    
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
            if (Parent == null) return;
            
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
    
    private enum PounceState
    {
        None,
        Approaching,
        Attacking,
        Wrapping
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
        
        public void Follow(float targetX, float targetY, float dist, float targetAngle,
            bool isPouncing, bool isCoiling, PounceState pounceState, bool isBrute, float sneakyLevel,
            float aggressionLevel, float deltaTimeMs)
        {
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
            
            // Adjust acceleration based on behavior and personality
            if (isCoiling)
            {
                accel *= 0.3f;
            }
            else if (pounceState == PounceState.Wrapping)
            {
                accel *= 1.8f + aggressionLevel * 0.3f;
            }
            else if (pounceState == PounceState.Attacking)
            {
                accel *= 4.0f + aggressionLevel * 1.0f;
            }
            else if (pounceState == PounceState.Approaching)
            {
                var basePounceSpeed = isBrute ? 2.5f : 2.0f;
                var aggressionBoost = aggressionLevel * (isBrute ? 1.0f : 0.5f);
                accel *= basePounceSpeed + aggressionBoost;
            }
            else if (isPouncing)
            {
                accel *= 2.0f;
            }
            else
            {
                if (isBrute)
                {
                    accel *= 0.8f + aggressionLevel * 0.3f;
                }
                else
                {
                    accel *= 0.7f + aggressionLevel * 0.2f * sneakyLevel;
                }
            }
            
            FSpeed += accel * (dist > FThresh ? 1 : 0);
            FSpeed *= 1f - FRes;
            Speed = MathF.Max(0, FSpeed - FFric);
            
            // Update rotation
            var dif = AbsAngle - targetAngle;
            dif -= 2f * MathF.PI * MathF.Floor(dif / (2f * MathF.PI) + 0.5f);
            
            var rotationAccel = RAccel;
            
            if (pounceState == PounceState.Wrapping)
            {
                rotationAccel = RAccel * 1.5f * (1 + aggressionLevel * 0.3f);
            }
            else if (pounceState == PounceState.Attacking)
            {
                rotationAccel = RAccel * 3.0f * (1 + aggressionLevel * 0.5f);
            }
            else if (pounceState == PounceState.Approaching)
            {
                rotationAccel = RAccel * (isBrute ? 2.5f : 2.0f) * (1 + aggressionLevel * 0.3f);
            }
            else if (isPouncing)
            {
                rotationAccel = RAccel * 2.0f * (1 + aggressionLevel * 0.3f);
            }
            else if (isCoiling)
            {
                rotationAccel = RAccel * 0.6f;
            }
            else
            {
                if (isBrute)
                {
                    rotationAccel = RAccel * (1 + aggressionLevel * 0.4f);
                }
                else
                {
                    rotationAccel = RAccel * (0.9f + aggressionLevel * 0.3f * sneakyLevel);
                }
            }
            
            if (MathF.Abs(dif) > RThresh && dist > FThresh)
            {
                RSpeed -= rotationAccel * (dif > 0 ? 1 : -1);
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
                system.Update(targetX, targetY);
            }
            AbsAngle -= MathF.PI;
        }
    }
    
    private Creature? critter;
    private int legCount;
    private float creatureSize;
    private Random random = null!;
    
    // ========== HUNTER/STALKER STATE ==========
    private List<CursorHistoryEntry> cursorHistory = new();
    private Vector2 lastMousePos = Vector2.Zero;
    private float cursorIdleTime = 0f;
    private float huntTimer = 0f;
    private Vector2 stalkPosition = Vector2.Zero;
    private float pounceTimer = 0f;
    private bool hasPounced = false;
    private float lastCoilTime = 0f;
    private float aggressionLevel = 0f;
    private float coilAngle = 0f;
    private PounceState pounceState = PounceState.None;
    private float wrapRadius = COIL_RADIUS;
    private DateTime lastFrameTime = DateTime.Now;
    
    public override void Initialize()
    {
        base.Initialize();
        random = new Random();
        
        var viewport = ImGui.GetMainViewport();
        var centerX = viewport.Size.X / 2f;
        var centerY = viewport.Size.Y / 2f;
        
        // Generate random creature parameters - matching JS exactly
        // JS: legCount = Math.floor(1 + Math.random() * 12); // 1-12 legs
        legCount = random.Next(1, 13); // 1-12 legs
        var baseSize = 8f / MathF.Sqrt(legCount);
        var sizeVariation = 0.8f + (float)random.NextDouble() * 0.4f;
        var scaledSize = baseSize * 0.2f * sizeVariation;
        
        // Calculate speed multiplier based on leg count (matching JS)
        // JS: const speedMultiplier = 0.5 + (legCount - 1) * (2.5 / 11);
        var speedMultiplier = 0.5f + (legCount - 1) * (2.5f / 11f);
        
        // JS: Math.floor(4 + Math.random() * legCount * 8)
        var tailLength = random.Next(4, legCount * 8 + 4);
        
        // Ensure even number of legs (half on each side) - done after tail calculation to match JS
        if (legCount % 2 != 0) legCount++;
        legCount = Math.Min(legCount, 12);
        legCount = Math.Max(legCount, 2);
        creatureSize = sizeVariation;
        
        SetupLizard(scaledSize, legCount, tailLength, speedMultiplier);
        
        // Initialize hunting state
        lastFrameTime = DateTime.Now;
        stalkPosition = new Vector2(centerX, centerY);
        cursorIdleTime = 0f;
        pounceTimer = 0f;
        hasPounced = false;
        cursorHistory.Clear();
        lastCoilTime = 0f;
        aggressionLevel = 0f;
        coilAngle = 0f;
        huntTimer = 0f;
        pounceState = PounceState.None;
        wrapRadius = COIL_RADIUS;
        lastMousePos = new Vector2(centerX, centerY);
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
        
        // Convert deltaTime from seconds to milliseconds for JS compatibility
        var deltaTimeMs = deltaTime * 1000f;
        
        // Calculate distance to actual cursor
        var distToCursor = Vector2.Distance(critter.Position, cursorPos);
        
        // ========== AGGRESSION SYSTEM ==========
        var currentTime = (float)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;
        var timeSinceLastCoil = currentTime - lastCoilTime;
        
        if (distToCursor > COIL_RADIUS * 2)
        {
            // Not coiled - increase aggression
            aggressionLevel = MathF.Min(1.0f, aggressionLevel + (deltaTimeMs * AGGRESSION_INCREASE_RATE));
        }
        else if (distToCursor < COIL_RADIUS && cursorIdleTime > IDLE_THRESHOLD)
        {
            // Successfully coiled! Reset aggression
            if (lastCoilTime == 0 || timeSinceLastCoil > 1000)
            {
                lastCoilTime = currentTime;
                aggressionLevel = 0;
            }
        }
        
        // Update hunt timer
        huntTimer += deltaTimeMs;
        
        // Determine personality based on leg count
        var legCountForPersonality = legCount;
        var isBrute = legCountForPersonality <= 4;
        var sneakyLevel = legCountForPersonality >= 8 ? 1.0f : MathF.Max(0, (legCountForPersonality - 4) / 4f);
        var isAnaconda = legCountForPersonality >= 8;
        
        // Update cursor tracking
        var mouseMoved = MathF.Abs(cursorPos.X - lastMousePos.X) > 1 || MathF.Abs(cursorPos.Y - lastMousePos.Y) > 1;
        var cursorMovementDist = Vector2.Distance(cursorPos, lastMousePos);
        
        if (mouseMoved && cursorMovementDist > IDLE_RADIUS)
        {
            // Cursor moved significantly - reset idle tracking
            cursorIdleTime = 0;
            pounceTimer = 0;
            hasPounced = false;
            cursorHistory.Add(new CursorHistoryEntry { Position = cursorPos, Time = currentTime });
            if (cursorHistory.Count > HISTORY_SIZE)
            {
                cursorHistory.RemoveAt(0);
            }
        }
        else
        {
            // Check if cursor is within idle radius
            if (cursorHistory.Count > 0)
            {
                var lastPos = cursorHistory[cursorHistory.Count - 1].Position;
                var distFromLastPos = Vector2.Distance(cursorPos, lastPos);
                if (distFromLastPos < IDLE_RADIUS)
                {
                    cursorIdleTime += deltaTimeMs;
                    pounceTimer += deltaTimeMs;
                }
                else
                {
                    cursorIdleTime = 0;
                    pounceTimer = 0;
                    hasPounced = false;
                }
            }
            else
            {
                cursorIdleTime += deltaTimeMs;
                pounceTimer += deltaTimeMs;
            }
        }
        
        lastMousePos = cursorPos;
        
        // ========== PERSONALITY-BASED STALKING BEHAVIOR ==========
        var baseStalkDistance = STALK_DISTANCE;
        
        if (isBrute)
        {
            baseStalkDistance = STALK_DISTANCE * (1 - aggressionLevel * 0.5f);
        }
        else
        {
            baseStalkDistance = STALK_DISTANCE * (1 + sneakyLevel * 0.3f);
            if (aggressionLevel > 0.5f)
            {
                var restlessOffset = MathF.Sin(aggressionLevel * MathF.PI * 4) * 40 * aggressionLevel;
                baseStalkDistance += restlessOffset;
            }
        }
        
        // Calculate ideal stalking position (behind cursor)
        var angleToCursor = MathF.Atan2(cursorPos.Y - stalkPosition.Y, cursorPos.X - stalkPosition.X);
        
        // Brute: Direct approach, Sneaky: Offset angle for indirect approach
        var stalkOffsetAngle = 0f;
        if (!isBrute && sneakyLevel > 0.3f)
        {
            stalkOffsetAngle = MathF.Sin(aggressionLevel * MathF.PI * 2 + huntTimer / 200f) * 0.4f * sneakyLevel;
        }
        
        var idealStalkX = cursorPos.X - MathF.Cos(angleToCursor + stalkOffsetAngle) * baseStalkDistance;
        var idealStalkY = cursorPos.Y - MathF.Sin(angleToCursor + stalkOffsetAngle) * baseStalkDistance;
        
        // Smoothly move stalk position towards ideal position (with lag)
        var currentLagSpeed = isBrute ? LAG_SPEED * 1.5f : LAG_SPEED * (0.7f + sneakyLevel * 0.3f);
        var stalkDist = Vector2.Distance(new Vector2(idealStalkX, idealStalkY), stalkPosition);
        if (stalkDist > 5)
        {
            stalkPosition.X += (idealStalkX - stalkPosition.X) * currentLagSpeed;
            stalkPosition.Y += (idealStalkY - stalkPosition.Y) * currentLagSpeed;
        }
        
        // ========== POUNCE BEHAVIOR ==========
        var adjustedPounceTrigger = isBrute
            ? POUNCE_TRIGGER * (1 - aggressionLevel * 0.5f)
            : POUNCE_TRIGGER * (1 + sneakyLevel * 0.3f);
        
        var isPouncing = false;
        var isCoiling = false;
        
        // Check if close enough to coil (even without pounce)
        var shouldCoil = distToCursor < COIL_RADIUS && cursorIdleTime > IDLE_THRESHOLD && pounceState == PounceState.None;
        
        // Update pounce state machine
        if (pounceTimer > adjustedPounceTrigger && !hasPounced && !shouldCoil)
        {
            if (pounceState == PounceState.None)
            {
                pounceState = PounceState.Approaching;
                hasPounced = true;
                isPouncing = true;
            }
        }
        
        // Transition from approaching to attacking/ouroboros when close
        if (pounceState == PounceState.Approaching && distToCursor < STALK_DISTANCE * 0.5f)
        {
            if (isAnaconda)
            {
                pounceState = PounceState.Wrapping;
                wrapRadius = COIL_RADIUS * 1.2f;
            }
            else
            {
                pounceState = PounceState.Attacking;
            }
        }
        
        // Transition from wrapping/attacking to coiling when very close and stable
        var isCloseEnough = isAnaconda
            ? (distToCursor < COIL_RADIUS * 2 && cursorIdleTime > 500)
            : (distToCursor < COIL_RADIUS * 0.5f);
        
        if ((pounceState == PounceState.Wrapping || pounceState == PounceState.Attacking) &&
            isCloseEnough && cursorIdleTime > 300)
        {
            if (isAnaconda && distToCursor < COIL_RADIUS * 1.5f)
            {
                // Keep ouroboros going
            }
            else if (!isAnaconda)
            {
                pounceState = PounceState.None;
                hasPounced = false;
                isCoiling = true;
                wrapRadius = COIL_RADIUS;
            }
        }
        
        // If cursor moves away during pounce, abort and reset
        if (pounceState != PounceState.None && distToCursor > STALK_DISTANCE * 1.5f)
        {
            pounceState = PounceState.None;
            hasPounced = false;
            isPouncing = false;
            wrapRadius = COIL_RADIUS;
        }
        
        // Normal idle coiling (when not pouncing)
        if (shouldCoil && pounceState == PounceState.None)
        {
            isCoiling = true;
        }
        
        // Calculate target position based on behavior
        var targetX = stalkPosition.X;
        var targetY = stalkPosition.Y;
        var targetAngle = MathF.Atan2(targetY - critter.Position.Y, targetX - critter.Position.X);
        var dist = Vector2.Distance(critter.Position, new Vector2(targetX, targetY));
        
        // ========== POUNCE BEHAVIORS ==========
        if (pounceState == PounceState.Wrapping)
        {
            // OUROBOROS MODE: Pace around cursor in tight perpetual circle
            isPouncing = true;
            
            var baseOuroborosRadius = COIL_RADIUS * 1.1f;
            var radiusVariation = MathF.Sin(aggressionLevel * MathF.PI * 2 + huntTimer / 500f) * (COIL_RADIUS * 0.2f);
            wrapRadius = baseOuroborosRadius + radiusVariation;
            
            var rotationSpeed = 0.004f + aggressionLevel * 0.002f;
            coilAngle += deltaTimeMs * rotationSpeed;
            if (coilAngle > MathF.PI * 2) coilAngle -= MathF.PI * 2;
            
            var circleX = cursorPos.X + MathF.Cos(coilAngle) * wrapRadius;
            var circleY = cursorPos.Y + MathF.Sin(coilAngle) * wrapRadius;
            
            targetX = circleX;
            targetY = circleY;
            targetAngle = MathF.Atan2(circleY - critter.Position.Y, circleX - critter.Position.X);
            dist = Vector2.Distance(critter.Position, new Vector2(circleX, circleY));
        }
        else if (pounceState == PounceState.Attacking)
        {
            // BRUTE MODE: Direct attack
            isPouncing = true;
            
            var attackOffset = MathF.Min(5, distToCursor * 0.1f);
            var angleToCursor2 = MathF.Atan2(cursorPos.Y - critter.Position.Y, cursorPos.X - critter.Position.X);
            targetX = cursorPos.X - MathF.Cos(angleToCursor2) * attackOffset;
            targetY = cursorPos.Y - MathF.Sin(angleToCursor2) * attackOffset;
            targetAngle = MathF.Atan2(targetY - critter.Position.Y, targetX - critter.Position.X);
            dist = distToCursor * 4.0f;
        }
        else if (pounceState == PounceState.Approaching)
        {
            // Still approaching during pounce
            isPouncing = true;
            
            if (isBrute)
            {
                targetX = cursorPos.X;
                targetY = cursorPos.Y;
            }
            else
            {
                var curveAmount = (1 - aggressionLevel) * 30;
                var angleToCursor2 = MathF.Atan2(cursorPos.Y - critter.Position.Y, cursorPos.X - critter.Position.X);
                var curveAngle = angleToCursor2 + MathF.Sin(huntTimer / 100f) * (curveAmount / STALK_DISTANCE);
                targetX = cursorPos.X - MathF.Cos(curveAngle) * curveAmount;
                targetY = cursorPos.Y - MathF.Sin(curveAngle) * curveAmount;
            }
            targetAngle = MathF.Atan2(targetY - critter.Position.Y, targetX - critter.Position.X);
            dist = distToCursor * (1.5f + aggressionLevel * 0.5f);
        }
        else if (isCoiling)
        {
            // Normal idle coiling: curl around cursor in a proper circle
            coilAngle += deltaTimeMs * 0.003f;
            if (coilAngle > MathF.PI * 2) coilAngle -= MathF.PI * 2;
            
            var coilRadius = COIL_RADIUS;
            var circleX = cursorPos.X + MathF.Cos(coilAngle) * coilRadius;
            var circleY = cursorPos.Y + MathF.Sin(coilAngle) * coilRadius;
            
            targetX = circleX;
            targetY = circleY;
            targetAngle = MathF.Atan2(circleY - critter.Position.Y, circleX - critter.Position.X);
            dist = Vector2.Distance(critter.Position, new Vector2(circleX, circleY)) * 0.3f;
        }
        
        // Update creature with full physics (matching JS exactly)
        critter.Follow(targetX, targetY, dist, targetAngle, isPouncing, isCoiling, pounceState, 
            isBrute, sneakyLevel, aggressionLevel, deltaTimeMs);
    }
    
    public override void Draw()
    {
        if (!isActive || critter == null) return;
        
        var drawList = ImGui.GetForegroundDrawList();
        
        // Match JS color: strokeColor based on theme
        // JS uses: isDarkMode ? '#e8e8d8' : '#000000'
        // For Dalamud (typically dark), use the bone color
        // #e8e8d8 = rgb(232, 232, 216)
        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0.91f, 0.91f, 0.85f, 1f)); // #e8e8d8 bone color
        
        // Draw root creature and all children (matching JS: critter.draw(true))
        DrawSegment(drawList, critter, color);
    }
    
    private void DrawSegment(ImDrawListPtr drawList, Segment segment, uint color)
    {
        // Draw line from parent to this segment (matching JS draw method exactly)
        // JS: ctx.lineWidth = 2, but canvas rendering differs from ImGui
        // Using 0.5f for very thin, delicate skeleton lines
        if (segment.Parent != null)
        {
            drawList.AddLine(segment.Parent.Position, segment.Position, color, 0.5f);
        }
        
        // Skip drawing joint for root creature (it has no parent and doesn't need a visible joint)
        // Only draw joint for segments that have a parent (matching JS behavior)
        if (segment.Parent == null)
        {
            // Root creature - just draw children
            foreach (var child in segment.Children)
            {
                DrawSegment(drawList, child, color);
            }
            return;
        }
        
        // Draw joint - matching JS exactly: partial arc (3/4 circle) + triangle
        // JS: var r = 4; but the creature is scaled to 1/5 size (0.2f), so effective radius is much smaller
        // Also, canvas lineWidth=2 renders thinner than ImGui, so we need smaller radius
        // For very small segments (like claws), skip joints entirely or make them tiny
        // The joint should be barely visible - just a small arc with a point, not a circle
        var absAngle = segment.Parent != null 
            ? MathF.Atan2(segment.Position.Y - segment.Parent.Position.Y, segment.Position.X - segment.Parent.Position.X)
            : segment.AbsAngle;
        
        // Skip drawing joints for very small segments (like claws) - they should just be lines
        // Small segments have size < 1.0 (like s * 0.1 or s * 3 for tiny details)
        if (segment.Size < 1.0f)
        {
            // Very small segment - just draw children, no joint
            foreach (var child in segment.Children)
            {
                DrawSegment(drawList, child, color);
            }
            return;
        }
        
        // For normal segments, draw a very small, subtle joint
        // Scale joint size based on segment size to keep proportions
        var r = MathF.Min(0.5f, segment.Size * 0.15f); // Very small joints, scaled by segment size
        
        // JS: ctx.arc(this.x, this.y, r, Math.PI / 4 + this.absAngle, 7 * Math.PI / 4 + this.absAngle);
        // Draw partial arc from PI/4 to 7*PI/4 (3/4 circle) - matching JS exactly
        var startAngle = MathF.PI / 4f + absAngle;
        var endAngle = 7f * MathF.PI / 4f + absAngle;
        
        // Draw arc using line segments (JS uses ctx.arc which creates a smooth arc)
        // Use fewer segments for a more subtle, less "circle-like" appearance
        var arcPoints = new List<Vector2>();
        const int arcSegments = 6; // Even fewer segments = less circle-like, more subtle
        for (int i = 0; i <= arcSegments; i++)
        {
            var t = i / (float)arcSegments;
            var angle = startAngle + t * (endAngle - startAngle);
            arcPoints.Add(new Vector2(
                segment.Position.X + r * MathF.Cos(angle),
                segment.Position.Y + r * MathF.Sin(angle)
            ));
        }
        
        // Draw arc outline (JS strokes the arc) - use very thin lines for subtle joints
        for (int i = 0; i < arcPoints.Count - 1; i++)
        {
            drawList.AddLine(arcPoints[i], arcPoints[i + 1], color, 0.5f);
        }
        
        // Draw triangle pointing in direction of movement (matching JS exactly)
        // This creates the small "point" at the joint, not a full circle
        // JS: ctx.moveTo(...), ctx.lineTo(...), ctx.lineTo(...), ctx.lineTo(...), ctx.stroke()
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
        
        // Draw triangle lines (JS strokes these) - very thin lines for subtle joints
        drawList.AddLine(endPoint, centerPoint, color, 0.5f);
        drawList.AddLine(centerPoint, startPoint, color, 0.5f);
        // Close the triangle (connect start to end) - JS stroke() closes the path
        drawList.AddLine(startPoint, endPoint, color, 0.5f);
        
        // Recursively draw children (matching JS: if (iter) { children[i].draw(true); })
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
        cursorHistory.Clear();
    }
}
