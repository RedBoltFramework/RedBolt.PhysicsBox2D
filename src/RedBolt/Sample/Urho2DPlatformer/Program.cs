// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Urho2DPlatformer;

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

internal static class Urho2DPlatformerProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DPlatformer(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Playable orthogonal TMX platformer using Spriter and Box2D.</summary>
public sealed class Urho2DPlatformer : Application
{
    private Node? _cameraNode;
    private Node? _character;
    private RigidBody2D? _characterBody;
    private PhysicsWorld2D? _world;
    private readonly Vector3f _initialSpawnPosition = new(1.5f, 3.4f, 0.0f);
    private Vector3f _spawnPosition = new(1.5f, 3.4f, 0.0f);
    private Vector2f _levelSize;
    private int _groundContacts;
    private bool _drawDebug;
    private bool _levelComplete;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DPlatformer";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData.2D;SampleData.2D;CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Platformer";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        scene.CreateComponent<DebugRenderer>();
        var zone = scene.CreateChild("Backdrop").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.24f, 0.28f, 0.29f));
        _world = scene.CreateComponent<PhysicsWorld2D>();
        _cameraNode = scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(1.05f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        if (Resources.Get<TmxFile2D>("Urho2D/Tilesets/Platformer/platformer.tmx") is { } tmx)
        {
            var tileMapNode = scene.CreateChild("TileMap");
            var tileMap = tileMapNode.CreateComponent<TileMap2D>();
            tileMap.TmxFile = tmx;
            _levelSize = new Vector2f(
                tileMap.Info.Width * tileMap.Info.TileWidth,
                tileMap.Info.Height * tileMap.Info.TileHeight);
            var utilities = new Sample2D(scene, Resources);
            _character = utilities.CreateCharacter(tileMap.Info, 0.0f, _spawnPosition, 0.30f);
            _characterBody = _character.GetComponent<RigidBody2D>();
            _characterBody!.FixedRotation = true;
            utilities.CreateCollisionShapesFromTmxObjects(tileMapNode, tileMap.GetLayer("Physics"), tileMap.Info);
            utilities.PopulateCoins(tileMap.GetLayer("Coins"));
            CreateTriggerNodes(scene, tileMap.GetLayer("Hazards"), "Hazard");
            CreateTriggerNodes(scene, tileMap.GetLayer("Checkpoints"), "Checkpoint");
            CreateTriggerNodes(scene, tileMap.GetLayer("Goal"), "Goal");
        }

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        SubscribeToEvent<PhysicsEvents2D.PhysicsBeginContact2DEvent>(_world, HandleBeginContact);
        SubscribeToEvent<PhysicsEvents2D.PhysicsEndContact2DEvent>(_world, HandleEndContact);
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) =>
        {
            if (_drawDebug) _world.DrawDebugGeometry();
        });
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        if (Input.GetKeyPress(Key.Z)) _drawDebug = !_drawDebug;
        if (_characterBody is null || _character is null || _cameraNode is null) return;
        if (Input.GetKeyPress(Key.R))
        {
            _spawnPosition = _initialSpawnPosition;
            _levelComplete = false;
            Respawn();
        }
        if (_character.GetWorldPosition().Y < -1.0f) Respawn();
        var velocity = _characterBody.LinearVelocity;
        velocity.X = 0.0f;
        if (!_levelComplete)
        {
            if (Input.GetKeyDown(Key.A)) velocity.X -= 4.0f;
            if (Input.GetKeyDown(Key.D)) velocity.X += 4.0f;
            if (Input.GetKeyPress(Key.Space) && _groundContacts > 0) velocity.Y = 7.0f;
        }
        else
            velocity = Vector2f.Zero;
        _characterBody.LinearVelocity = velocity;
        if (_character.GetComponent<AnimatedSprite2D>() is { } sprite)
        {
            if (velocity.X != 0.0f) sprite.FlipX = velocity.X > 0.0f;
            sprite.SetAnimation(_groundContacts == 0 ? "idle" : velocity.X == 0.0f ? "idle" : "run");
        }
        var camera = _cameraNode.GetComponent<Camera>()!;
        if (Input.GetKeyDown(Key.PageUp)) camera.SetZoom(camera.Zoom * 1.01f);
        if (Input.GetKeyDown(Key.PageDown)) camera.SetZoom(camera.Zoom * 0.99f);
        var cameraPosition = _character.GetWorldPosition() + new Vector3f(0.0f, 1.5f, -10.0f);
        var halfHeight = camera.OrthoSize / (2.0f * camera.Zoom);
        var halfWidth = halfHeight * Graphics.Width / Graphics.Height;
        cameraPosition.X = Math.Clamp(cameraPosition.X, halfWidth, _levelSize.X - halfWidth);
        cameraPosition.Y = Math.Clamp(cameraPosition.Y, halfHeight, _levelSize.Y - halfHeight);
        _cameraNode.SetPosition(cameraPosition);
    }

    private void HandleBeginContact(ref PhysicsEvents2D.PhysicsBeginContact2DEvent value)
    {
        var other = ReferenceEquals(value.NodeA, _character) ? value.NodeB
            : ReferenceEquals(value.NodeB, _character) ? value.NodeA : null;
        if (other is null) return;
        if (other.Name == "Coin") other.Remove();
        else if (other.Name == "Hazard") Respawn();
        else if (other.Name == "Checkpoint")
        {
            var position = other.GetWorldPosition();
            _spawnPosition = new Vector3f(position.X, position.Y + 0.8f, 0.0f);
            other.Remove();
        }
        else if (other.Name == "Goal")
        {
            _levelComplete = true;
            Console.WriteLine("Relay Foundry 7 evacuated. Press R to restart.");
        }
        else if (!value.ShapeA.Trigger && !value.ShapeB.Trigger) ++_groundContacts;
    }

    private void HandleEndContact(ref PhysicsEvents2D.PhysicsEndContact2DEvent value)
    {
        if ((ReferenceEquals(value.NodeA, _character) || ReferenceEquals(value.NodeB, _character)) &&
            !value.ShapeA.Trigger && !value.ShapeB.Trigger)
            _groundContacts = Math.Max(0, _groundContacts - 1);
    }

    private static void CreateTriggerNodes(Scene scene, TileMapLayer2D? layer, string nodeName)
    {
        if (layer is null)
            return;
        for (uint index = 0; index < layer.NumObjects; ++index)
        {
            if (layer.GetObject(index) is not { ObjectType: TileMapObjectType2D.Rectangle } value)
                continue;
            var node = scene.CreateChild(nodeName);
            node.SetPosition2D(value.Position + value.Size * 0.5f);
            node.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Static;
            var shape = node.CreateComponent<CollisionBox2D>();
            shape.Size = value.Size;
            shape.Trigger = true;
        }
    }

    private void Respawn()
    {
        if (_character is null || _characterBody is null)
            return;
        _character.SetPosition(_spawnPosition);
        _characterBody.LinearVelocity = Vector2f.Zero;
        _groundContacts = 0;
    }
}
