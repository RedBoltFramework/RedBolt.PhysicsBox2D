// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Android;

internal static class SampleCatalog
{
    internal static readonly SampleEntry[] Entries =
    [
        new("physics2d", "Physics2D", static () => new global::RedBolt.Sample.Physics2D.Physics2D()),
        new("physics2d-rope", "Physics2D Rope", static () => new global::RedBolt.Sample.Physics2DRope.Physics2DRope()),
        new("physics2d-constraints", "Physics2D Constraints", static () => new global::RedBolt.Sample.Physics2DConstraints.Physics2DConstraints()),
        new("urho2d-isometric-demo", "Urho2D Isometric Demo", static () => new global::RedBolt.Sample.Urho2DIsometricDemo.Urho2DIsometricDemo()),
        new("urho2d-platformer", "Urho2D Platformer", static () => new global::RedBolt.Sample.Urho2DPlatformer.Urho2DPlatformer()),
    ];
}
