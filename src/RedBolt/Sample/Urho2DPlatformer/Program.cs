// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Urho2DPlatformer;

using System;
using System.Collections.Generic;
using System.Text;
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
using RedBolt.Sample.Utilities2D;
using RedBolt.Scene;
using RedBolt.Scene2D;
using Widgets = RedBolt.SystemUI.Widgets;

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
    private SampleInputMap? _sampleInput;
    private const float RunSpeed = 4.6f;
    private const float JumpSpeed = 7.2f;
    private const float CoyoteDuration = 0.12f;
    private const float JumpBufferDuration = 0.12f;
    private const float DeathDuration = 1.4f;
    private const int RelayCount = 4;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);
    private static readonly Vector4f HudBackground = new(0.025f, 0.045f, 0.065f, 0.94f);
    private static readonly Vector4f HudBorder = new(0.18f, 0.55f, 0.68f, 0.85f);
    private static readonly Vector4f HudAccent = new(0.25f, 0.85f, 1.0f, 1.0f);
    private static readonly Vector4f HudSuccess = new(0.35f, 0.95f, 0.55f, 1.0f);
    private static readonly Vector4f HudMessage = new(1.0f, 0.78f, 0.22f, 1.0f);

    private readonly Vector3f _initialSpawnPosition = new(1.5f, 3.4f, 0.0f);
    private readonly HashSet<CollisionShape2D> _groundShapes = [];
    private readonly List<Node> _coins = [];
    private readonly List<Node> _checkpoints = [];
    private Node? _cameraNode;
    private Node? _character;
    private RigidBody2D? _characterBody;
    private PhysicsWorld2D? _world;
    private Texture2D[]? _controlPrompts;
    private Vector3f _spawnPosition = new(1.5f, 3.4f, 0.0f);
    private Vector2f _levelSize;
    private float _coyoteTimer;
    private float _deathTimer;
    private float _jumpBufferTimer;
    private float _messageTimer = 6.0f;
    private int _collectedCells;
    private int _relaysRestored;
    private bool _spaceWasDown;
    private bool _cameraInitialized;
    private bool _drawDebug;
    private bool _dying;
    private bool _levelComplete;
    private string _message = "Follow the gold relay cells to the first blue relay.";

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
        _sampleInput = new SampleInputMap(movement: true);
        _sampleInput.AddButton("Debug", Scancode.G, ControllerButton.Y, "DEBUG", 1);
        _sampleInput.AddButton("Restart", Scancode.Return, ControllerButton.X, "RESTART", 2);
        _sampleInput.AddButton("Jump", Scancode.Space, ControllerButton.A, "JUMP");
        _sampleInput.AddButton("ZoomIn", Scancode.PageUp, ControllerButton.RightShoulder, "+", 3);
        _sampleInput.AddButton("ZoomOut", Scancode.PageDown, ControllerButton.LeftShoulder, "-", 4);
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        scene.CreateComponent<DebugRenderer>();
        var zone = scene.CreateChild("Backdrop").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.12f, 0.17f, 0.21f));
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
            _levelSize = new Vector2f(tileMap.Info.Width * tileMap.Info.TileWidth, tileMap.Info.Height * tileMap.Info.TileHeight);
            var utilities = new Sample2D(scene, Resources);
            _character = utilities.CreateCharacter(tileMap.Info, 0.0f, _spawnPosition, 0.30f);
            _characterBody = _character.GetComponent<RigidBody2D>();
            _characterBody!.FixedRotation = true;
            _character.RemoveComponent<CollisionCircle2D>();
            var characterShape = _character.CreateComponent<CollisionBox2D>();
            characterShape.Size = new Vector2f(1.45f, 2.4f);
            characterShape.Center = new Vector2f(0.0f, 0.12f);
            characterShape.Friction = 0.0f;
            utilities.CreateCollisionShapesFromTmxObjects(tileMapNode, tileMap.GetLayer("Physics"), tileMap.Info);
            PopulateRelayCells(scene, tileMap.GetLayer("Coins"));
            CreateTriggerNodes(scene, tileMap.GetLayer("Hazards"), "Hazard", null);
            CreateTriggerNodes(scene, tileMap.GetLayer("Checkpoints"), "Checkpoint",
                "Urho2D/Tilesets/Platformer/Sprites/laserSwitchBlueOn.png", _checkpoints);
            CreateTriggerNodes(scene, tileMap.GetLayer("Goal"), "Goal",
                "Urho2D/Tilesets/Platformer/Sprites/laserGreenBurst.png");
        }

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_enter.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_g.png")
        ];
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawHud());
        SubscribeToEvent<PhysicsEvents2D.PhysicsBeginContact2DEvent>(_world, HandleBeginContact);
        SubscribeToEvent<PhysicsEvents2D.PhysicsEndContact2DEvent>(_world, HandleEndContact);
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
        if (_sampleInput.Pressed("Restart")) RestartLevel();
        if (_character.GetWorldPosition().Y < -1.0f) BeginDeath();

        _messageTimer = MathF.Max(0.0f, _messageTimer - timeStep);
        if (_dying)
        {
            _deathTimer -= timeStep;
            if (_deathTimer <= 0.0f) Respawn("The last relay pulled you back to safety.");
            return;
        }
        _coyoteTimer = _groundShapes.Count > 0 ? CoyoteDuration : MathF.Max(0.0f, _coyoteTimer - timeStep);
        _jumpBufferTimer = MathF.Max(0.0f, _jumpBufferTimer - timeStep);
        var spaceDown = _sampleInput.Evaluate("Jump") > 0.0f;
        if (_sampleInput.Pressed("Jump")) _jumpBufferTimer = JumpBufferDuration;

        var velocity = _characterBody.LinearVelocity;
        var direction = 0.0f;
        if (!_levelComplete)
        {
            direction -= _sampleInput.Evaluate("Left");
            direction += _sampleInput.Evaluate("Right");
        }
        var acceleration = _groundShapes.Count > 0 ? 34.0f : 18.0f;
        velocity.X = MoveTowards(velocity.X, direction * RunSpeed, acceleration * timeStep);
        if (_jumpBufferTimer > 0.0f && _coyoteTimer > 0.0f && !_levelComplete)
        {
            velocity.Y = JumpSpeed;
            _jumpBufferTimer = 0.0f;
            _coyoteTimer = 0.0f;
            _groundShapes.Clear();
        }
        if (_spaceWasDown && !spaceDown && velocity.Y > 0.0f) velocity.Y *= 0.48f;
        if (_levelComplete) velocity.X = MoveTowards(velocity.X, 0.0f, 40.0f * timeStep);
        _spaceWasDown = spaceDown;
        _characterBody.LinearVelocity = velocity;

        if (_character.GetComponent<AnimatedSprite2D>() is { } sprite)
        {
            if (MathF.Abs(velocity.X) > 0.1f) sprite.FlipX = velocity.X > 0.0f;
            var animation = _groundShapes.Count == 0 || MathF.Abs(velocity.X) < 0.15f ? "idle" : "run";
            if (sprite.Animation != animation) sprite.SetAnimation(animation);
        }

        var camera = _cameraNode.GetComponent<Camera>()!;
        if (_sampleInput.Evaluate("ZoomIn") > 0.0f) camera.SetZoom(camera.Zoom * 1.01f);
        if (_sampleInput.Evaluate("ZoomOut") > 0.0f) camera.SetZoom(camera.Zoom * 0.99f);
        var target = _character.GetWorldPosition() + new Vector3f(velocity.X * 0.38f, 1.35f, -10.0f);
        var halfHeight = camera.OrthoSize / (2.0f * camera.Zoom);
        var halfWidth = halfHeight * Graphics.Width / Graphics.Height;
        target.X = Math.Clamp(target.X, halfWidth, _levelSize.X - halfWidth);
        target.Y = Math.Clamp(target.Y, halfHeight, _levelSize.Y - halfHeight);
        var current = _cameraNode.GetPosition();
        var blend = _cameraInitialized ? 1.0f - MathF.Exp(-7.0f * timeStep) : 1.0f;
        _cameraNode.SetPosition(current + (target - current) * blend);
        _cameraInitialized = true;
    }

    private void HandleBeginContact(ref PhysicsEvents2D.PhysicsBeginContact2DEvent value)
    {
        var characterIsA = ReferenceEquals(value.NodeA, _character);
        var other = characterIsA ? value.NodeB : ReferenceEquals(value.NodeB, _character) ? value.NodeA : null;
        if (other is null) return;
        if (other.Name == "Coin")
        {
            other.SetEnabled(false);
            ++_collectedCells;
            ShowMessage("Relay cell secured - keep moving!", 1.3f);
        }
        else if (other.Name == "Hazard") BeginDeath();
        else if (other.Name == "Checkpoint") ActivateCheckpoint(other);
        else if (other.Name == "Goal")
        {
            if (_relaysRestored == RelayCount)
            {
                _levelComplete = true;
                ShowMessage("EVACUATION COMPLETE - Relay Foundry 7 lives!", 60.0f);
            }
            else ShowMessage($"Beacon locked: restore {RelayCount - _relaysRestored} more relay(s).", 2.5f);
        }
        else if (!value.ShapeA.Trigger && !value.ShapeB.Trigger && IsGroundContact(value, characterIsA))
            _groundShapes.Add(characterIsA ? value.ShapeB : value.ShapeA);
    }

    private void HandleEndContact(ref PhysicsEvents2D.PhysicsEndContact2DEvent value)
    {
        if (ReferenceEquals(value.NodeA, _character)) _groundShapes.Remove(value.ShapeB);
        else if (ReferenceEquals(value.NodeB, _character)) _groundShapes.Remove(value.ShapeA);
    }

    private static bool IsGroundContact(PhysicsEvents2D.PhysicsBeginContact2DEvent value, bool characterIsA)
    {
        foreach (var contact in value.Contacts)
        {
            var normalY = characterIsA ? contact.Normal.Y : -contact.Normal.Y;
            if (normalY < -0.55f) return true;
        }
        return false;
    }

    private void PopulateRelayCells(Scene scene, TileMapLayer2D? layer)
    {
        if (layer is null) return;
        for (uint index = 0; index < layer.NumObjects; ++index)
        {
            if (layer.GetObject(index) is not { } value) continue;
            var node = scene.CreateChild("Coin");
            node.SetScale(0.5f);
            node.SetPosition2D(value.Position + value.Size * 0.5f + new Vector2f(0.0f, 0.16f));
            var sprite = node.CreateComponent<StaticSprite2D>();
            sprite.Sprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/shieldGold.png");
            sprite.Layer = 4;
            node.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Static;
            var shape = node.CreateComponent<CollisionCircle2D>();
            shape.Radius = 0.32f;
            shape.Trigger = true;
            _coins.Add(node);
        }
    }

    private void CreateTriggerNodes(Scene scene, TileMapLayer2D? layer, string nodeName, string? spritePath,
        List<Node>? destination = null)
    {
        if (layer is null) return;
        for (uint index = 0; index < layer.NumObjects; ++index)
        {
            if (layer.GetObject(index) is not { ObjectType: TileMapObjectType2D.Rectangle } value) continue;
            var node = scene.CreateChild(nodeName);
            node.SetPosition2D(value.Position + value.Size * 0.5f);
            node.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Static;
            CollisionShape2D shape;
            if (value.Name.StartsWith("Spikes_", StringComparison.Ordinal))
            {
                var halfSize = value.Size * 0.5f;
                var polygon = node.CreateComponent<CollisionPolygon2D>();
                polygon.SetVertices([
                    new Vector2f(-halfSize.X, -halfSize.Y),
                    new Vector2f(0.03f, halfSize.Y),
                    new Vector2f(halfSize.X, -halfSize.Y),
                ]);
                shape = polygon;
            }
            else
            {
                var box = node.CreateComponent<CollisionBox2D>();
                box.Size = value.Size;
                shape = box;
            }
            shape.Trigger = true;
            if (spritePath is not null)
            {
                var sprite = node.CreateComponent<StaticSprite2D>();
                sprite.Sprite = Resources.Get<Sprite2D>(spritePath);
                sprite.Layer = 4;
                HideSwitchTopArtifact(sprite);
            }
            destination?.Add(node);
        }
    }

    private void ActivateCheckpoint(Node checkpoint)
    {
        checkpoint.SetName("ActivatedCheckpoint");
        var position = checkpoint.GetWorldPosition();
        _spawnPosition = new Vector3f(position.X, position.Y + 0.9f, 0.0f);
        ++_relaysRestored;
        if (checkpoint.GetComponent<StaticSprite2D>() is { } marker)
        {
            marker.Sprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/laserSwitchGreenOn.png");
            marker.Color = new Color(0.7f, 1.0f, 0.75f);
        }
        ShowMessage($"Relay {_relaysRestored}/{RelayCount} restored. Respawn point updated.", 2.2f);
    }

    private void RestartLevel()
    {
        _spawnPosition = _initialSpawnPosition;
        _collectedCells = 0;
        _relaysRestored = 0;
        _levelComplete = false;
        foreach (var coin in _coins) coin.SetEnabled(true);
        foreach (var checkpoint in _checkpoints)
        {
            checkpoint.SetName("Checkpoint");
            if (checkpoint.GetComponent<StaticSprite2D>() is { } marker)
            {
                marker.Sprite = Resources.Get<Sprite2D>("Urho2D/Tilesets/Platformer/Sprites/laserSwitchBlueOn.png");
                marker.Color = Color.White;
            }
        }
        Respawn("Run restarted. The evacuation beacon is waiting.");
    }

    private void Respawn(string message)
    {
        if (_character is null || _characterBody is null) return;
        _dying = false;
        _character.SetPosition(_spawnPosition);
        _characterBody.BodyType = BodyType2D.Dynamic;
        _characterBody.LinearVelocity = Vector2f.Zero;
        if (_character.GetComponent<AnimatedSprite2D>() is { } sprite) sprite.SetAnimation("idle");
        _groundShapes.Clear();
        _coyoteTimer = 0.0f;
        _jumpBufferTimer = 0.0f;
        ShowMessage(message, 2.0f);
    }

    private void BeginDeath()
    {
        if (_dying || _character is null || _characterBody is null) return;
        _dying = true;
        _deathTimer = DeathDuration;
        _characterBody.LinearVelocity = Vector2f.Zero;
        _characterBody.BodyType = BodyType2D.Kinematic;
        _groundShapes.Clear();
        if (_character.GetComponent<AnimatedSprite2D>() is { } sprite)
            sprite.SetAnimation("dead", LoopMode2D.ForceClamped);
        ShowMessage("SYSTEM FAILURE - relay recovery engaged...", DeathDuration);
    }

    private void ShowMessage(string message, float duration)
    {
        _message = message;
        _messageTimer = duration;
    }

    private void DrawHud()
    {
        var open = true;
        ImGui.SetNextWindowPos(new Vector2f(12.0f, 12.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(new Vector2f(520.0f, 104.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 9.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(14.0f, 10.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, HudBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, HudBorder);
        if (ImGui.Begin("Relay Foundry HUD"u8, ref open,
                ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration | ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, HudAccent);
            ImGui.TextUnformatted("RELAY FOUNDRY 7  //  EVAC ROUTE"u8);
            ImGui.PopStyleColor();
            ImGui.SameLine(310.0f);
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, _relaysRestored == RelayCount ? HudSuccess : HudMessage);
            DrawText($"RELAYS {_relaysRestored}/{RelayCount}   CELLS {_collectedCells}/{_coins.Count}");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.TextUnformatted(_levelComplete ? "FOUNDRY STABLE  //  Press ENTER to run again"u8 :
                "GOLD CELLS  >  BLUE RELAYS  >  GREEN BEACON"u8);
            ImGui.Image(_controlPrompts![0], GetPromptSize(_controlPrompts[0]));
            ImGui.SameLine();
            ImGui.Image(_controlPrompts[1], GetPromptSize(_controlPrompts[1]));
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted("MOVE"u8);
            ImGui.SameLine();
            ImGui.Image(_controlPrompts[2], GetPromptSize(_controlPrompts[2]));
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted("JUMP"u8);
            ImGui.SameLine();
            ImGui.Image(_controlPrompts[3], GetPromptSize(_controlPrompts[3]));
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted("RESTART"u8);
            ImGui.SameLine();
            ImGui.Image(_controlPrompts[4], GetPromptSize(_controlPrompts[4]));
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted("COLLIDERS"u8);
            if (_messageTimer > 0.0f)
            {
                ImGui.PushStyleColor(ImGuiCol.ImGuiColText, HudMessage);
                DrawText(_message);
                ImGui.PopStyleColor();
            }
        }
        ImGui.End();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

    private static void HideSwitchTopArtifact(StaticSprite2D sprite)
    {
        if (sprite.Sprite is not { Texture: { } texture } source ||
            !source.GetTextureRectangle(out var textureRectangle))
            return;
        textureRectangle.Max.Y = (1.0f + source.TextureEdgeOffset) / texture.Height;
        sprite.TextureRect = textureRectangle;
        sprite.UseTextureRect = true;
    }

    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        if (MathF.Abs(target - current) <= maximumDelta) return target;
        return current + MathF.CopySign(maximumDelta, target - current);
    }

    private static Vector2f GetPromptSize(Texture2D texture) =>
        new(PromptIconSize.Y * texture.Width / texture.Height, PromptIconSize.Y);

    private static void DrawText(string text) => ImGui.TextUnformatted(Encoding.UTF8.GetBytes(text));
    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        base.Stop();
    }

}
