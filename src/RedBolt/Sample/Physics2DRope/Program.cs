// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Physics2DRope;

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

internal static class Physics2DRopeProgram
{
    private static int Main()
    {
        try { using var application = new Physics2DRope(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Interactive revolute-joint chain constrained by a maximum-length safety cable.</summary>
public sealed class Physics2DRope : Application
{
    private SampleInputMap? _sampleInput;
    private const float AnchorHeight = 15.0f;
    private const float RopeLength = 9.0f;
    private const float RopeMaxLength = 9.2f;
    private const float PayloadSpringLength = 0.08f;
    private const int LinkCount = 9;
    private static readonly Vector2f PromptHeight = new(24.0f, 24.0f);
    private static readonly Vector4f HudBackground = new(0.025f, 0.045f, 0.065f, 0.94f);
    private static readonly Vector4f HudBorder = new(0.18f, 0.55f, 0.68f, 0.85f);
    private static readonly Vector4f HudAccent = new(0.25f, 0.85f, 1.0f, 1.0f);
    private static readonly Vector4f HudStatus = new(1.0f, 0.78f, 0.22f, 1.0f);

    private readonly List<(Node Node, RigidBody2D Body)> _chainBodies = [];
    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _payloadNode;
    private Node? _safetyCable;
    private StaticSprite2D? _safetyCableSprite;
    private PhysicsWorld2D? _world;
    private RigidBody2D? _payloadBody;
    private ConstraintMouse2D? _mouseJoint;
    private Texture2D[]? _controlPrompts;
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics2DRope";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData.2D;SampleData.2D;CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics2D Rope Lab";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        _sampleInput.AddButton("Reset", Scancode.Return, ControllerButton.A, "RESET");
        _sampleInput.AddButton("Debug", Scancode.G, ControllerButton.Y, "DEBUG", 1);
        _sampleInput.AddButton("Launch", Scancode.Space, ControllerButton.X, "LAUNCH", 2);
        _sampleInput.AddMouseButton("Grab", MouseButton.Left, ControllerButton.B, "GRAB", 3);
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        CreateScene();
        CreateInstructions();
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit")) { Exit(); return; }
        if (_cameraNode is null || _world is null || _mouseJoint is null || _payloadBody is null) return;
        if (_sampleInput.Pressed("Reset")) ResetExperiment();
        if (_sampleInput.Pressed("Debug")) _drawDebug = !_drawDebug;
        if (_sampleInput.Pressed("Launch"))
        {
            var direction = _payloadNode!.GetWorldPosition().X >= 0.0f ? -1.0f : 1.0f;
            _payloadBody.LinearVelocity += new Vector2f(direction * 4.0f, 3.0f);
            _payloadBody.Awake = true;
        }

        var camera = _cameraNode.GetComponent<Camera>()!;
        if (_mouseJoint.OtherBody is null && _sampleInput.Evaluate("Grab") > 0.0f &&
            !ImGui.GetIO().WantCaptureMouse)
        {
            var target = GetMouseWorldPosition(camera);
            if (_world.GetRigidBody(target) is { BodyType: BodyType2D.Dynamic } body)
            {
                _mouseJoint.Target = target;
                _mouseJoint.OtherBody = body;
                body.Awake = true;
            }
        }
        if (_mouseJoint.OtherBody is not null)
        {
            if (_sampleInput.Evaluate("Grab") > 0.0f)
                _mouseJoint.Target = GetMouseWorldPosition(camera);
            else
                _mouseJoint.OtherBody = null;
        }
        UpdateSafetyCable();
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();
        var zone = _scene.CreateChild("Backdrop").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.055f, 0.085f, 0.12f));
        _world = _scene.CreateComponent<PhysicsWorld2D>();
        _world.DrawJoint = true;

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 7.5f, -10.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * 0.05f;
        camera.SetZoom(1.35f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        var ground = _scene.CreateChild("Ground");
        var groundBody = ground.CreateComponent<RigidBody2D>();
        ground.CreateComponent<CollisionEdge2D>().SetVertices(new Vector2f(-40.0f, 0.0f), new Vector2f(40.0f, 0.0f));
        CreateFoundryBackdrop();

        var linkSprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metalMid.png");
        var previousBody = groundBody;
        _chainBodies.Clear();
        for (var i = 0; i <= LinkCount; ++i)
        {
            var isPayload = i == LinkCount;
            var node = _scene.CreateChild(isPayload ? "Payload" : $"Link {i + 1}");
            node.SetPosition2D(isPayload ? i : 0.5f + i, AnchorHeight);
            var body = node.CreateComponent<RigidBody2D>();
            body.BodyType = BodyType2D.Dynamic;
            body.LinearDamping = 0.3f;
            var box = node.CreateComponent<CollisionBox2D>();
            box.Friction = 0.25f;
            box.MaskBits = 0xffff & ~0x0002;
            if (isPayload)
            {
                body.AngularDamping = 0.4f;
                box.Size = new Vector2f(3.0f, 3.0f);
                box.Density = 70.0f;
                box.CategoryBits = 0x0002;
                CreatePayloadVisual(node);
                _payloadNode = node;
                _payloadBody = body;
            }
            else
            {
                box.Size = new Vector2f(1.0f, 0.25f);
                box.Density = 20.0f;
                box.CategoryBits = 0x0001;
                CreateSprite(node, linkSprite, new Rect(-0.5f, -0.13f, 0.5f, 0.13f), 2);
            }
            if (isPayload)
            {
                // Keep the links inextensible, but give the heavy payload a short damped
                // coupling. The safety cable remains slack below its nominal length and
                // only catches the small amount of extension allowed under tension.
                var joint = node.CreateComponent<ConstraintDistance2D>();
                joint.OtherBody = previousBody;
                joint.OwnerBodyAnchor = new Vector2f(i, AnchorHeight);
                joint.OtherBodyAnchor = new Vector2f(i, AnchorHeight);
                joint.Length = PayloadSpringLength;
                joint.FrequencyHz = 40.0f;
                joint.DampingRatio = 0.9f;
                joint.CollideConnected = false;
            }
            else
            {
                var joint = node.CreateComponent<ConstraintRevolute2D>();
                joint.OtherBody = previousBody;
                joint.Anchor = new Vector2f(i, AnchorHeight);
                joint.CollideConnected = false;
            }
            previousBody = body;
            _chainBodies.Add((node, body));
        }

        var rope = ground.CreateComponent<ConstraintRope2D>();
        rope.OtherBody = previousBody;
        rope.OwnerBodyAnchor = new Vector2f(0.0f, AnchorHeight);
        var payloadPosition = _payloadNode!.GetWorldPosition();
        rope.OtherBodyAnchor = new Vector2f(payloadPosition.X, payloadPosition.Y);
        rope.MaxLength = RopeMaxLength;
        _mouseJoint = ground.CreateComponent<ConstraintMouse2D>();
        // The payload is intentionally much heavier than the links, so the mouse joint needs
        // enough force to track normal pointer movement instead of feeling unresponsive.
        _mouseJoint.MaxForce = 120000.0f;
        _mouseJoint.FrequencyHz = 7.0f;
        _mouseJoint.DampingRatio = 0.9f;

        _safetyCable = _scene.CreateChild("Safety Cable");
        _safetyCableSprite = CreateSprite(_safetyCable,
            Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metalMid.png"),
            new Rect(-0.5f, -0.045f, 0.5f, 0.045f), 1);
        _safetyCableSprite.Color = new Color(0.25f, 0.85f, 1.0f, 0.72f);
        UpdateSafetyCable();

        Renderer.SetViewport(0, new Viewport(_scene, camera));
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>(
            (ref CoreEvents.PostRenderUpdateEvent _) => { if (_drawDebug) _world.DrawDebugGeometry(); });
    }

    private void CreateFoundryBackdrop()
    {
        var floorSprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metalCenter.png");
        for (var x = -26; x <= 26; ++x)
        {
            var tile = _scene!.CreateChild("Floor Tile");
            tile.SetPosition2D(x * 0.7f, -0.35f);
            CreateSprite(tile, floorSprite, new Rect(-0.35f, -0.35f, 0.35f, 0.35f), -1);
        }
        var beamSprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metalMid.png");
        foreach (var x in new[] { -14.0f, -7.0f, 14.0f })
        {
            for (var y = 0.35f; y < 16.0f; y += 0.7f)
            {
                var support = _scene!.CreateChild("Background Support");
                support.SetPosition2D(x, y);
                var drawable = CreateSprite(support, beamSprite, new Rect(-0.35f, -0.35f, 0.35f, 0.35f), -8);
                drawable.Color = new Color(0.10f, 0.18f, 0.24f, 0.72f);
            }
        }
        for (var x = -20; x <= 20; ++x)
        {
            var rail = _scene!.CreateChild("Overhead Rail");
            rail.SetPosition2D(x * 0.7f, 16.1f);
            var drawable = CreateSprite(rail, beamSprite, new Rect(-0.35f, -0.35f, 0.35f, 0.35f), -7);
            drawable.Color = new Color(0.13f, 0.22f, 0.29f, 0.82f);
        }
        var hook = _scene!.CreateChild("Anchor Hook");
        hook.SetPosition2D(0.0f, AnchorHeight + 0.38f);
        CreateSprite(hook, Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/wireHook.png"),
            new Rect(-0.55f, -0.55f, 0.55f, 0.55f), 3);
    }

    private void CreatePayloadVisual(Node payload)
    {
        var sprites = new[]
        {
            Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metalCenterWarning.png"),
            Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metalCenterSticker.png"),
        };
        for (var i = 0; i < 4; ++i)
        {
            var tile = payload.CreateChild("Payload Panel");
            tile.SetPosition2D(i % 2 == 0 ? -0.75f : 0.75f, i < 2 ? 0.75f : -0.75f);
            CreateSprite(tile, sprites[i % 2], new Rect(-0.75f, -0.75f, 0.75f, 0.75f), 3);
        }
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

    private void UpdateSafetyCable()
    {
        if (_payloadNode is null || _safetyCable is null || _safetyCableSprite is null) return;
        var payload = _payloadNode.GetWorldPosition();
        var delta = new Vector2f(payload.X, payload.Y - AnchorHeight);
        var length = MathF.Max(0.01f, delta.Length);
        _safetyCable.SetPosition2D(delta.X * 0.5f, AnchorHeight + delta.Y * 0.5f);
        _safetyCable.SetRotation2D(MathF.Atan2(delta.Y, delta.X) * MathDefs.RadToDeg);
        _safetyCableSprite.DrawRect = new Rect(-length * 0.5f, -0.045f, length * 0.5f, 0.045f);
    }

    private void ResetExperiment()
    {
        _mouseJoint!.OtherBody = null;
        for (var i = 0; i < _chainBodies.Count; ++i)
        {
            var (node, body) = _chainBodies[i];
            node.SetPosition2D(i == LinkCount ? i : 0.5f + i, AnchorHeight);
            node.SetRotation2D(0.0f);
            body.LinearVelocity = Vector2f.Zero;
            body.AngularVelocity = 0.0f;
            body.Awake = true;
        }
        UpdateSafetyCable();
    }

    private Vector2f GetMouseWorldPosition(Camera camera)
    {
        var mouse = Input.GetMousePosition();
        var world = camera.ScreenToWorldPoint(new Vector3f(mouse.X / (float)Graphics.Width,
            mouse.Y / (float)Graphics.Height, 10.0f));
        return new Vector2f(world.X, world.Y);
    }

    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_left.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_enter.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_g.png"),
        ];
    }

    private void DrawInstructions()
    {
        var open = true;
        ImGui.SetNextWindowPos(new Vector2f(12.0f, 12.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(new Vector2f(500.0f, 86.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 9.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(14.0f, 10.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, HudBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, HudBorder);
        if (ImGui.Begin("Rope Lab HUD"u8, ref open,
                ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration | ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, HudAccent);
            ImGui.TextUnformatted("FOUNDRY LOAD TEST  //  REVOLUTE CHAIN + SAFETY CABLE"u8);
            ImGui.PopStyleColor();
            ImGui.Separator();
            DrawPrompt(_controlPrompts![0], "DRAG"u8);
            ImGui.SameLine();
            DrawPrompt(_controlPrompts[1], "KICK"u8);
            ImGui.SameLine();
            DrawPrompt(_controlPrompts[2], "RESET"u8);
            ImGui.SameLine();
            DrawPrompt(_controlPrompts[3], "JOINTS"u8);
            var payload = _payloadNode?.GetWorldPosition() ?? Vector3f.Zero;
            var distance = new Vector2f(payload.X, payload.Y - AnchorHeight).Length;
            var extension = MathF.Max(0.0f, distance - RopeLength);
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, HudStatus);
            DrawText($"CABLE LOAD  {Math.Clamp(distance / RopeLength * 100.0f, 0.0f, 100.0f):000}%   " +
                $"STRETCH  {extension * 100.0f:00} cm   " +
                $"PAYLOAD SPEED  {_payloadBody?.LinearVelocity.Length ?? 0.0f:0.0} m/s");
            ImGui.PopStyleColor();
        }
        ImGui.End();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, GetPromptSize(texture));
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static Vector2f GetPromptSize(Texture2D texture) =>
        new(PromptHeight.Y * texture.Width / texture.Height, PromptHeight.Y);

    private static void DrawText(string text) => ImGui.TextUnformatted(System.Text.Encoding.UTF8.GetBytes(text));
    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        base.Stop();
    }

}
