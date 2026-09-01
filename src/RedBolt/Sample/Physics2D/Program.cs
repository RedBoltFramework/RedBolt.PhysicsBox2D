// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Physics2D;

using System;
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

internal static class Physics2DProgram
{
    private static int Main()
    {
        try { using var application = new Physics2D(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates sprites driven by Box2D rigid bodies.</summary>
public sealed class Physics2D : Application
{
    private SampleInputMap? _sampleInput;
    private Scene? _scene;
    private Node? _cameraNode;
    private PhysicsWorld2D? _physicsWorld;
    private ConstraintMouse2D? _mouseJoint;
    private Texture2D[]? _controlPrompts;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics2D";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData.2D;SampleData.2D;CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics2D";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap(movement: true);
        _sampleInput.AddButton("ZoomIn", Scancode.PageUp, ControllerButton.RightShoulder, "+");
        _sampleInput.AddButton("ZoomOut", Scancode.PageDown, ControllerButton.LeftShoulder, "-", 1);
        _sampleInput.AddMouseButton("Grab", MouseButton.Left, ControllerButton.A, "GRAB", 2);
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();
        _physicsWorld = _scene.CreateComponent<PhysicsWorld2D>();
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(1.2f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        var boxSprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/metal.png");
        var ballSprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/shieldGold.png");
        var ground = _scene.CreateChild("Ground");
        ground.SetPosition2D(0.0f, -3.0f);
        ground.SetScale(new Vector3f(200.0f, 1.0f, 1.0f));
        ground.CreateComponent<RigidBody2D>();
        ground.CreateComponent<StaticSprite2D>().Sprite = boxSprite;
        var groundShape = ground.CreateComponent<CollisionBox2D>();
        groundShape.Size = GetSpriteSize(boxSprite);
        groundShape.Friction = 0.5f;

        for (var i = 0; i < 24; ++i)
        {
            var node = _scene.CreateChild("RigidBody");
            node.SetPosition2D(-3.0f + i % 8 * 0.85f + MathDefs.Random(-0.03f, 0.03f),
                -1.9f + i / 8 * 0.85f);
            node.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Dynamic;
            var drawable = node.CreateComponent<StaticSprite2D>();
            if (i % 2 == 0)
            {
                drawable.Sprite = boxSprite;
                var box = node.CreateComponent<CollisionBox2D>();
                box.Size = GetSpriteSize(boxSprite);
                SetShapeProperties(box);
            }
            else
            {
                drawable.Sprite = ballSprite;
                var shield = node.CreateComponent<CollisionPolygon2D>();
                shield.SetVertices([
                    new Vector2f(-0.18f, 0.25f), new Vector2f(0.18f, 0.25f),
                    new Vector2f(0.18f, -0.08f), new Vector2f(0.12f, -0.20f),
                    new Vector2f(0.0f, -0.25f), new Vector2f(-0.12f, -0.20f),
                    new Vector2f(-0.18f, -0.08f),
                ]);
                SetShapeProperties(shield);
            }
        }

        _mouseJoint = ground.CreateComponent<ConstraintMouse2D>();
        _mouseJoint.MaxForce = 80.0f;
        _mouseJoint.FrequencyHz = 5.0f;
        _mouseJoint.DampingRatio = 0.8f;

        CreateInstructions();
        Renderer.SetViewport(0, new Viewport(_scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit")) { Exit(); return; }
        const float speed = 4.0f;
        var movement = Vector2f.Zero;
        movement.Y += (_sampleInput.Evaluate("Forward") - _sampleInput.Evaluate("Back")) * speed * timeStep;

        movement.X += (_sampleInput.Evaluate("Right") - _sampleInput.Evaluate("Left")) * speed * timeStep;

        _cameraNode!.Translate2D(movement);
        var camera = _cameraNode.GetComponent<Camera>()!;
        if (_sampleInput.Evaluate("ZoomIn") > 0.0f) camera.SetZoom(camera.Zoom * 1.01f);
        if (_sampleInput.Evaluate("ZoomOut") > 0.0f) camera.SetZoom(camera.Zoom * 0.99f);

        if (_mouseJoint!.OtherBody is null && _sampleInput.Pressed("Grab") &&
            !ImGui.GetIO().WantCaptureMouse)
        {
            var mouse = Input.GetMousePosition();
            if (_physicsWorld!.GetRigidBody(mouse.X, mouse.Y) is { BodyType: BodyType2D.Dynamic } body)
            {
                _mouseJoint.Target = GetMouseWorldPosition(camera);
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
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_arrows.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_left.png"),
        ];
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Physics 2D"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            for (var i = 0; i < 3; ++i)
            {
                ImGui.Image(_controlPrompts![i], PromptIconSize);
                ImGui.SameLine();
            }
            DrawPrompt(_controlPrompts![3], "Move camera"u8);
            DrawPrompt(_controlPrompts[4], "Zoom (Page Up / Page Down)"u8);
            DrawPrompt(_controlPrompts[5], "Drag objects"u8);
        }
        ImGui.End();
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, PromptIconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static Vector2f GetSpriteSize(Sprite2D sprite) =>
        new(sprite.Rectangle.Width * Sprite2D.PixelSize, sprite.Rectangle.Height * Sprite2D.PixelSize);

    private static void SetShapeProperties(CollisionShape2D shape)
    {
        shape.Density = 1.0f;
        shape.Friction = 0.5f;
        shape.Restitution = 0.1f;
    }
    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        base.Stop();
    }

}
