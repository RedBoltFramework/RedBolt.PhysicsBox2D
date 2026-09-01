// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Urho2DIsometricDemo;

using System;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Resource;
using RedBolt.Sample.Utilities2D;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Urho2DIsometricDemoProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DIsometricDemo(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Playable isometric TMX, Spriter and Box2D demonstration.</summary>
public sealed class Urho2DIsometricDemo : Application
{
    private SampleInputMap? _sampleInput;
    private Node? _cameraNode;
    private Node? _character;
    private RigidBody2D? _characterBody;
    private StaticSprite2D? _characterSprite;
    private Sprite2D[][] _idleFrames = [];
    private Sprite2D[][] _runFrames = [];
    private int _characterDirection = 3;
    private float _characterAnimationTime;
    private PhysicsWorld2D? _world;
    private float _verticalSpeedScale = 0.5f;
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DIsometricDemo";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData.2D;SampleData.2D;CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Isometric Demo - F2: Physics Collisions";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap(movement: true);
        _sampleInput.AddButton("Debug", Scancode.F2, ControllerButton.Y, "DEBUG");
        _sampleInput.Map.MapKeyboardKey("Debug", Scancode.Z);
        _sampleInput.AddButton("ZoomIn", Scancode.PageUp, ControllerButton.RightShoulder, "+", 1);
        _sampleInput.AddButton("ZoomOut", Scancode.PageDown, ControllerButton.LeftShoulder, "-", 2);
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        scene.CreateComponent<DebugRenderer>();
        var zone = scene.CreateChild("Backdrop").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.72f, 0.58f, 0.43f));
        _world = scene.CreateComponent<PhysicsWorld2D>();
        _world.Gravity = Vector2f.Zero;
        _cameraNode = scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(0.55f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        if (Resources.Get<TmxFile2D>("Urho2D/Tilesets/IsometricDemo/dungeon.tmx") is { } tmx)
        {
            var tileMapNode = scene.CreateChild("TileMap");
            var tileMap = tileMapNode.CreateComponent<TileMap2D>();
            tileMap.TmxFile = tmx;
            var info = tileMap.Info;
            var utilities = new Sample2D(scene, Resources);
            _idleFrames = LoadCharacterFrames("Idle", 1);
            _runFrames = LoadCharacterFrames("Run", 10);
            var start = info.TileIndexToPosition(10, 10);
            _character = CreateMaleCharacter(scene, new Vector3f(start.X, start.Y, 0.0f));
            _characterBody = _character.GetComponent<RigidBody2D>();
            _verticalSpeedScale = info.TileWidth != 0.0f ? info.TileHeight / info.TileWidth : 0.5f;
            utilities.CreateCollisionShapesFromTmxObjects(tileMapNode, tileMap.GetLayer("Physics"), info);
        }

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) =>
        {
            if (_drawDebug) _world.DrawDebugGeometry();
        });
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit")) { Exit(); return; }
        if (_sampleInput.Pressed("Debug")) _drawDebug = !_drawDebug;
        if (_characterBody is null || _character is null || _cameraNode is null) return;
        const float speed = 3.0f;
        var velocity = Vector2f.Zero;
        velocity.Y += (_sampleInput.Evaluate("Forward") - _sampleInput.Evaluate("Back")) * speed * _verticalSpeedScale;

        velocity.X += (_sampleInput.Evaluate("Right") - _sampleInput.Evaluate("Left")) * speed;

        _characterBody.LinearVelocity = velocity;
        if (_characterSprite is not null)
        {
            var moving = velocity != Vector2f.Zero;
            if (moving)
                _characterDirection = GetDirectionIndex(velocity);
            var frames = moving ? _runFrames[_characterDirection] : _idleFrames[_characterDirection];
            if (moving)
                _characterAnimationTime += timeStep;
            var frame = moving ? (int)(_characterAnimationTime * 12.0f) % frames.Length : 0;
            _characterSprite.Sprite = frames[frame];
            _characterSprite.Layer = -(int)(_character.GetWorldPosition().Y * 100.0f);
        }
        _cameraNode.SetPosition(_character.GetWorldPosition() + new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = _cameraNode.GetComponent<Camera>()!;
        if (_sampleInput.Evaluate("ZoomIn") > 0.0f) camera.SetZoom(camera.Zoom * 1.01f);
        if (_sampleInput.Evaluate("ZoomOut") > 0.0f) camera.SetZoom(camera.Zoom * 0.99f);
    }

    private Sprite2D[][] LoadCharacterFrames(string animation, int count)
    {
        var directions = new Sprite2D[8][];
        for (var direction = 0; direction < directions.Length; ++direction)
        {
            var frames = new Sprite2D[count];
            for (var index = 0; index < count; ++index)
            {
                var path = $"Urho2D/Characters/Male/Male_{direction}_{animation}{index}.png";
                frames[index] = Resources.Get<Sprite2D>(path)
                    ?? throw new InvalidOperationException($"Could not load isometric character frame '{path}'.");
                frames[index].TextureEdgeOffset = 1.0f;
            }
            directions[direction] = frames;
        }
        return directions;
    }

    // Kenney indices follow the isometric compass: 0=N, 2=E, 4=S, and 6=W.
    private static int GetDirectionIndex(Vector2f velocity) => velocity.Y switch
    {
        > 0.0f => velocity.X < 0.0f ? 6 : velocity.X > 0.0f ? 0 : 7,
        < 0.0f => velocity.X < 0.0f ? 4 : velocity.X > 0.0f ? 2 : 3,
        _ => velocity.X < 0.0f ? 5 : 1,
    };

    private Node CreateMaleCharacter(Scene scene, Vector3f position)
    {
        var node = scene.CreateChild("Characters").CreateChild("Male");
        node.SetPosition(position);
        var sprite = node.CreateComponent<StaticSprite2D>();
        sprite.Sprite = _idleFrames[_characterDirection][0];
        sprite.UseHotSpot = true;
        sprite.HotSpot = new Vector2f(0.5f, 0.1f);
        sprite.Layer = -(int)(position.Y * 100.0f);
        sprite.OrderInLayer = 15;
        _characterSprite = sprite;
        node.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Dynamic;
        node.GetComponent<RigidBody2D>()!.AllowSleep = false;
        var shape = node.CreateComponent<CollisionCircle2D>();
        shape.Radius = 0.30f;
        shape.Friction = 0.0f;
        shape.Restitution = 0.1f;
        return node;
    }
    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        base.Stop();
    }

}
