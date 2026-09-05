// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Physics2DConstraints;

using System;
using System.Collections.Generic;
using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class Physics2DConstraintsProgram
{
    private static int Main()
    {
        try { using var application = new Physics2DConstraints(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Interactive gallery of every 2D constraint supported by Box2D 3.</summary>
public sealed class Physics2DConstraints : Application
{
    private SampleInputMap? _sampleInput;
    private static readonly Color Accent = new(0.25f, 0.85f, 1.0f);
    private static readonly Vector4f HudBackground = new(0.025f, 0.045f, 0.065f, 0.94f);
    private static readonly Vector4f HudBorder = new(0.18f, 0.55f, 0.68f, 0.85f);
    private static readonly Vector4f HudAccent = new(0.25f, 0.85f, 1.0f, 1.0f);
    private readonly List<BodyState> _bodyStates = [];
    private readonly List<LinkVisual> _linkVisuals = [];
    private Scene? _scene;
    private PhysicsWorld2D? _world;
    private ConstraintMouse2D? _mouseConstraint;
    private ConstraintPrismatic2D? _prismaticConstraint;
    private Node? _mouseTargetMarker;
    private Texture2D[]? _controlPrompts;
    private float _elapsed;
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics2DConstraints";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData.2D;SampleData.2D;CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics2D Constraint Lab";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        _sampleInput.AddButton("Reset", Scancode.Return, ControllerButton.A, "RESET");
        _sampleInput.AddButton("Debug", Scancode.G, ControllerButton.Y, "DEBUG", 1);
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        CreateScene();
        CreateInstructions();
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();
        var zone = _scene.CreateChild("Backdrop").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.035f, 0.06f, 0.085f));
        _world = _scene.CreateComponent<PhysicsWorld2D>();
        _world.DrawJoint = true;

        var cameraNode = _scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, -0.15f, -10.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(0.9f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        CreateBackdrop();
        CreateDistance(new Vector2f(-4.4f, 2.25f));
        CreateFriction(new Vector2f(0.0f, 2.25f));
        CreateMotor(new Vector2f(4.4f, 2.25f));
        CreateMouse(new Vector2f(-4.4f, -0.05f));
        CreatePrismatic(new Vector2f(0.0f, -0.05f));
        CreateRevolute(new Vector2f(4.4f, -0.05f));
        CreateRope(new Vector2f(-4.4f, -2.35f));
        CreateWeld(new Vector2f(0.0f, -2.35f));
        CreateWheel(new Vector2f(4.4f, -2.35f));

        Renderer.SetViewport(0, new Viewport(_scene, camera));
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) =>
        {
            if (_drawDebug) _world.DrawDebugGeometry();
        });
        UpdateVisuals();
    }

    private void CreateBackdrop()
    {
        var panelSprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metalCenter.png");
        foreach (var y in new[] { 2.25f, -0.05f, -2.35f })
        foreach (var x in new[] { -4.4f, 0.0f, 4.4f })
        {
            var panel = _scene!.CreateChild("Constraint panel");
            panel.SetPosition2D(x, y - 0.05f);
            var drawable = CreateSprite(panel, panelSprite, new Rect(-2.0f, -0.92f, 2.0f, 0.92f), -10);
            drawable.Color = new Color(0.08f, 0.14f, 0.18f, 0.82f);
        }
    }

    private (Node Node, RigidBody2D Body) CreateBody(string name, Vector2f position, BodyType2D type,
        bool circle = false, Vector2f? size = null)
    {
        var bodySize = size ?? new Vector2f(0.56f, 0.56f);
        var node = _scene!.CreateChild(name);
        node.SetPosition2D(position);
        var body = node.CreateComponent<RigidBody2D>();
        body.BodyType = type;
        body.LinearDamping = 0.1f;
        body.AngularDamping = 0.15f;
        if (circle)
        {
            CreateSprite(node, Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/shieldGold.png"),
                CenteredRect(bodySize), 2);
            var shape = node.CreateComponent<CollisionCircle2D>();
            shape.Radius = MathF.Min(bodySize.X, bodySize.Y) * 0.44f;
            SetShapeProperties(shape);
        }
        else
        {
            var drawable = CreateSprite(node, Resources.Get<Sprite2D>(
                "Urho2D/Tilesets/Platformer/Sprites/metalCenterWarning.png"), CenteredRect(bodySize), 2);
            if (type == BodyType2D.Static)
                drawable.Color = new Color(0.48f, 0.68f, 0.72f);
            var shape = node.CreateComponent<CollisionBox2D>();
            shape.Size = bodySize;
            SetShapeProperties(shape);
        }
        if (type == BodyType2D.Dynamic)
            _bodyStates.Add(new BodyState(node, body, position, 0.0f));
        return (node, body);
    }

    private void CreateDistance(Vector2f origin)
    {
        CreateLabel("DISTANCE  /  RADIUS", origin);
        var anchor = CreateBody("Distance anchor", origin + new Vector2f(-0.35f, 0.05f), BodyType2D.Static);
        var weight = CreateBody("Distance weight", origin + new Vector2f(0.35f, -0.35f), BodyType2D.Dynamic, true);
        var joint = anchor.Node.CreateComponent<ConstraintDistance2D>();
        joint.OtherBody = weight.Body;
        joint.OwnerBodyAnchor = anchor.Node.GetPosition2D();
        joint.OtherBodyAnchor = weight.Node.GetPosition2D();
        joint.Length = 0.8f;
        AddLink(anchor.Node, weight.Node, new Color(0.30f, 0.85f, 1.0f, 0.8f));
    }

    private void CreateFriction(Vector2f origin)
    {
        CreateLabel("FRICTION  /  BRAKE", origin);
        var anchor = CreateBody("Friction reference", origin, BodyType2D.Static, false, new Vector2f(0.2f, 0.2f));
        anchor.Node.GetComponent<StaticSprite2D>()!.SetEnabled(false);
        var puck = CreateBody("Friction puck", origin + new Vector2f(-1.15f, -0.25f), BodyType2D.Dynamic, true);
        var joint = anchor.Node.CreateComponent<ConstraintFriction2D>();
        joint.OtherBody = puck.Body;
        joint.Anchor = origin;
        joint.MaxForce = 1.2f;
        joint.MaxTorque = 0.8f;
        CreateRail(origin + new Vector2f(0.0f, -0.58f), new Vector2f(2.8f, 0.12f), true);
        SetInitialVelocity(puck.Body, new Vector2f(2.2f, 0.0f), 5.0f);
    }

    private void CreateMotor(Vector2f origin)
    {
        CreateLabel("MOTOR  /  SERVO", origin);
        var anchor = CreateBody("Motor reference", origin + new Vector2f(-0.7f, -0.45f), BodyType2D.Static);
        var follower = CreateBody("Motor follower", origin + new Vector2f(0.65f, 0.15f), BodyType2D.Dynamic, true);
        follower.Node.SetRotation2D(25.0f);
        UpdateSavedRotation(follower.Node, 25.0f);
        var joint = anchor.Node.CreateComponent<ConstraintMotor2D>();
        joint.OtherBody = follower.Body;
        joint.LinearOffset = new Vector2f(1.2f, 0.0f);
        joint.MaxForce = 12.0f;
        joint.MaxTorque = 5.0f;
        joint.CorrectionFactor = 0.25f;
        AddLink(anchor.Node, follower.Node, new Color(0.95f, 0.58f, 0.18f, 0.75f));
    }

    private void CreateMouse(Vector2f origin)
    {
        CreateLabel("MOUSE  /  TARGET", origin);
        var reference = CreateBody("Mouse reference", origin, BodyType2D.Static, false, new Vector2f(0.2f, 0.2f));
        reference.Node.GetComponent<StaticSprite2D>()!.SetEnabled(false);
        // A mouse joint grabs the local point that is under its initial target. Start the
        // body at that point so the moving marker behaves like a cursor, not a long lever.
        var follower = CreateBody("Mouse follower", origin + new Vector2f(0.0f, -0.02f), BodyType2D.Dynamic, true);
        follower.Body.GravityScale = 0.0f;
        _mouseTargetMarker = _scene!.CreateChild("Mouse target");
        _mouseTargetMarker.SetPosition2D(origin + new Vector2f(0.0f, -0.02f));
        var marker = CreateSprite(_mouseTargetMarker,
            Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/laserBlueBurst.png"),
            new Rect(-0.22f, -0.22f, 0.22f, 0.22f), 3);
        marker.Color = Accent;
        _mouseConstraint = reference.Node.CreateComponent<ConstraintMouse2D>();
        _mouseConstraint.OtherBody = follower.Body;
        _mouseConstraint.Target = _mouseTargetMarker.GetPosition2D();
        _mouseConstraint.MaxForce = 150.0f;
        _mouseConstraint.FrequencyHz = 6.0f;
        _mouseConstraint.DampingRatio = 0.8f;
        AddLink(_mouseTargetMarker, follower.Node, new Color(0.30f, 0.85f, 1.0f, 0.65f));
    }

    private void CreatePrismatic(Vector2f origin)
    {
        CreateLabel("PRISMATIC  /  SLIDER", origin);
        var anchor = CreateBody("Prismatic frame", origin + new Vector2f(-0.9f, -0.25f), BodyType2D.Static);
        var slider = CreateBody("Prismatic slider", origin + new Vector2f(-0.55f, -0.25f), BodyType2D.Dynamic,
            false, new Vector2f(0.65f, 0.5f));
        CreateRail(origin + new Vector2f(0.0f, -0.25f), new Vector2f(2.5f, 0.12f));
        var joint = anchor.Node.CreateComponent<ConstraintPrismatic2D>();
        joint.OtherBody = slider.Body;
        joint.Anchor = slider.Node.GetPosition2D();
        joint.Axis = Vector2f.Right;
        joint.EnableLimit = true;
        joint.LowerTranslation = 0.0f;
        joint.UpperTranslation = 1.8f;
        joint.EnableMotor = true;
        joint.MaxMotorForce = 8.0f;
        joint.MotorSpeed = 0.8f;
        _prismaticConstraint = joint;
    }

    private void CreateRevolute(Vector2f origin)
    {
        CreateLabel("REVOLUTE  /  HINGE", origin);
        var pivot = origin + new Vector2f(-0.65f, -0.35f);
        var anchor = CreateBody("Revolute anchor", pivot, BodyType2D.Static, true, new Vector2f(0.48f, 0.48f));
        var arm = CreateBody("Revolute arm", pivot + new Vector2f(0.75f, 0.0f), BodyType2D.Dynamic,
            false, new Vector2f(1.5f, 0.28f));
        var joint = anchor.Node.CreateComponent<ConstraintRevolute2D>();
        joint.OtherBody = arm.Body;
        joint.Anchor = pivot;
        joint.EnableMotor = true;
        joint.MotorSpeed = 0.75f;
        joint.MaxMotorTorque = 8.0f;
        joint.EnableLimit = true;
        joint.LowerAngle = -0.9f;
        joint.UpperAngle = 0.15f;
    }

    private void CreateRope(Vector2f origin)
    {
        CreateLabel("ROPE  /  LIMIT", origin);
        var anchor = CreateBody("Rope anchor", origin + new Vector2f(-0.55f, 0.1f), BodyType2D.Static);
        var weight = CreateBody("Rope weight", origin + new Vector2f(-0.55f, -0.6f), BodyType2D.Dynamic, true);
        var joint = anchor.Node.CreateComponent<ConstraintRope2D>();
        joint.OtherBody = weight.Body;
        joint.OwnerBodyAnchor = anchor.Node.GetPosition2D();
        joint.OtherBodyAnchor = weight.Node.GetPosition2D();
        joint.MaxLength = 0.7f;
        AddLink(anchor.Node, weight.Node, new Color(0.95f, 0.75f, 0.25f, 0.85f));
        SetInitialVelocity(weight.Body, new Vector2f(0.8f, 0.0f), 0.0f);
    }

    private void CreateWeld(Vector2f origin)
    {
        CreateLabel("WELD  /  FIXED", origin);
        var anchor = CreateBody("Weld anchor", origin + new Vector2f(-0.65f, -0.2f), BodyType2D.Static);
        var arm = CreateBody("Weld arm", origin + new Vector2f(0.45f, -0.2f), BodyType2D.Dynamic,
            false, new Vector2f(1.65f, 0.34f));
        arm.Node.SetRotation2D(-10.0f);
        UpdateSavedRotation(arm.Node, -10.0f);
        var joint = anchor.Node.CreateComponent<ConstraintWeld2D>();
        joint.OtherBody = arm.Body;
        joint.Anchor = origin + new Vector2f(-0.25f, -0.2f);
        joint.FrequencyHz = 8.0f;
        joint.DampingRatio = 0.8f;
    }

    private void CreateWheel(Vector2f origin)
    {
        CreateLabel("WHEEL  /  SUSPENSION", origin);
        var chassis = CreateBody("Wheel chassis", origin + new Vector2f(0.0f, 0.25f), BodyType2D.Static,
            false, new Vector2f(2.0f, 0.42f));
        var wheel = CreateBody("Wheel", origin + new Vector2f(0.0f, -0.48f), BodyType2D.Dynamic, true,
            new Vector2f(0.78f, 0.78f));
        var joint = chassis.Node.CreateComponent<ConstraintWheel2D>();
        joint.OtherBody = wheel.Body;
        joint.Anchor = wheel.Node.GetPosition2D();
        joint.Axis = Vector2f.Up;
        joint.EnableMotor = true;
        joint.MaxMotorTorque = 8.0f;
        joint.MotorSpeed = 2.5f;
        joint.FrequencyHz = 4.0f;
        joint.DampingRatio = 0.65f;
    }

    private void CreateLabel(string text, Vector2f origin)
    {
        var node = _scene!.CreateChild($"{text} label");
        node.SetPosition(new Vector3f(origin.X, origin.Y + 0.62f, 0.0f));
        var label = node.CreateComponent<Text3D>();
        label.SetText(text);
        label.SetFont(Resources.Get<Font>("Fonts/Anonymous Pro.ttf"), 18.0f);
        label.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);
        label.SetColor(Accent);
    }

    private void CreateRail(Vector2f position, Vector2f size, bool collidable = false)
    {
        var rail = _scene!.CreateChild("Guide rail");
        rail.SetPosition2D(position);
        var drawable = CreateSprite(rail, Resources.Get<Sprite2D>(
            "Urho2D/Tilesets/Platformer/Sprites/metalMid.png"), CenteredRect(size), 0);
        drawable.Color = new Color(0.22f, 0.52f, 0.6f);
        if (collidable)
        {
            rail.CreateComponent<RigidBody2D>();
            var shape = rail.CreateComponent<CollisionBox2D>();
            shape.Size = size;
            shape.Friction = 0.35f;
        }
    }

    private void AddLink(Node start, Node end, Color color)
    {
        var node = _scene!.CreateChild("Constraint link");
        var sprite = CreateSprite(node, Resources.Get<Sprite2D>(
            "Urho2D/Tilesets/Platformer/Sprites/metalMid.png"),
            new Rect(-0.5f, -0.035f, 0.5f, 0.035f), 1);
        sprite.Color = color;
        _linkVisuals.Add(new LinkVisual(node, sprite, start, end));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit")) { Exit(); return; }
        if (_sampleInput.Pressed("Reset")) ResetGallery();
        if (_sampleInput.Pressed("Debug")) _drawDebug = !_drawDebug;

        _elapsed += timeStep;
        if (_mouseTargetMarker is not null && _mouseConstraint is not null)
        {
            var target = new Vector2f(-4.4f + MathF.Sin(_elapsed * 1.15f) * 0.75f,
                -0.25f + MathF.Cos(_elapsed * 1.7f) * 0.18f);
            _mouseTargetMarker.SetPosition2D(target);
            _mouseConstraint.Target = target;
            _mouseTargetMarker.SetRotation2D(_elapsed * 90.0f);
        }
        if (_prismaticConstraint is not null)
        {
            var desiredSpeed = MathF.Sin(_elapsed * 0.8f) >= 0.0f ? 0.8f : -0.8f;
            if (_prismaticConstraint.MotorSpeed != desiredSpeed)
                _prismaticConstraint.MotorSpeed = desiredSpeed;
        }
        UpdateVisuals();
    }

    private void ResetGallery()
    {
        _elapsed = 0.0f;
        foreach (var state in _bodyStates)
        {
            state.Node.SetPosition2D(state.Position);
            state.Node.SetRotation2D(state.Rotation);
            state.Body.LinearVelocity = state.LinearVelocity;
            state.Body.AngularVelocity = state.AngularVelocity;
            state.Body.Awake = true;
        }
        UpdateVisuals();
    }

    private void SetInitialVelocity(RigidBody2D body, Vector2f linearVelocity, float angularVelocity)
    {
        body.LinearVelocity = linearVelocity;
        body.AngularVelocity = angularVelocity;
        for (var i = 0; i < _bodyStates.Count; ++i)
        {
            if (!ReferenceEquals(_bodyStates[i].Body, body)) continue;
            _bodyStates[i] = _bodyStates[i] with
            {
                LinearVelocity = linearVelocity,
                AngularVelocity = angularVelocity,
            };
            break;
        }
    }

    private void UpdateSavedRotation(Node node, float rotation)
    {
        for (var i = 0; i < _bodyStates.Count; ++i)
        {
            if (!ReferenceEquals(_bodyStates[i].Node, node)) continue;
            _bodyStates[i] = _bodyStates[i] with { Rotation = rotation };
            break;
        }
    }

    private void UpdateVisuals()
    {
        foreach (var link in _linkVisuals)
        {
            var start = link.Start.GetWorldPosition();
            var end = link.End.GetWorldPosition();
            var delta = new Vector2f(end.X - start.X, end.Y - start.Y);
            var length = MathF.Max(delta.Length, 0.01f);
            link.Node.SetPosition2D(start.X + delta.X * 0.5f, start.Y + delta.Y * 0.5f);
            link.Node.SetRotation2D(MathF.Atan2(delta.Y, delta.X) * MathDefs.RadToDeg);
            link.Sprite.DrawRect = new Rect(-length * 0.5f, -0.035f, length * 0.5f, 0.035f);
        }
    }

    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_enter.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_g.png"),
        ];
    }

    private void DrawInstructions()
    {
        var open = true;
        ImGui.SetNextWindowPos(new Vector2f(12.0f, 12.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(new Vector2f(560.0f, 70.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 9.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(14.0f, 10.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, HudBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, HudBorder);
        if (ImGui.Begin("Constraint Lab HUD"u8, ref open,
                ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration | ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, HudAccent);
            ImGui.TextUnformatted("BOX2D 3  //  CONSTRAINT LAB"u8);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextUnformatted("Nine live joint configurations"u8);
            ImGui.Separator();
            DrawPrompt(SampleInputMap.GetActionPrompt(_controlPrompts![0], ControllerButton.A), "RESET"u8);
            ImGui.SameLine();
            DrawPrompt(SampleInputMap.GetActionPrompt(_controlPrompts[1], ControllerButton.Y),
                _drawDebug ? "HIDE JOINTS"u8 : "SHOW JOINTS"u8);
        }
        ImGui.End();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        const float height = 24.0f;
        ImGui.Image(texture, new Vector2f(height * texture.Width / texture.Height, height));
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static StaticSprite2D CreateSprite(Node node, Sprite2D sprite, Rect drawRect, int layer)
    {
        var drawable = node.CreateComponent<StaticSprite2D>();
        drawable.Sprite = sprite;
        drawable.UseDrawRect = true;
        drawable.DrawRect = drawRect;
        drawable.Layer = layer;
        return drawable;
    }

    private static Rect CenteredRect(Vector2f size) =>
        new(-size.X * 0.5f, -size.Y * 0.5f, size.X * 0.5f, size.Y * 0.5f);

    private static void SetShapeProperties(CollisionShape2D shape)
    {
        shape.Density = 1.0f;
        shape.Friction = 0.45f;
        shape.Restitution = 0.05f;
    }

    private record struct BodyState(Node Node, RigidBody2D Body, Vector2f Position, float Rotation,
        Vector2f LinearVelocity = default, float AngularVelocity = 0.0f);
    private sealed record LinkVisual(Node Node, StaticSprite2D Sprite, Node Start, Node End);
    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        base.Stop();
    }

}
