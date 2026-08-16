// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Web;

using RedBolt.Engine;

internal static class SampleCatalog
{
    internal static Application Create(string applicationTypeName) => applicationTypeName switch
    {
        "RedBolt.Sample.Physics2D.Physics2D" => new global::RedBolt.Sample.Physics2D.Physics2D(),
        "RedBolt.Sample.Physics2DRope.Physics2DRope" => new global::RedBolt.Sample.Physics2DRope.Physics2DRope(),
        "RedBolt.Sample.Physics2DConstraints.Physics2DConstraints" => new global::RedBolt.Sample.Physics2DConstraints.Physics2DConstraints(),
        "RedBolt.Sample.Urho2DIsometricDemo.Urho2DIsometricDemo" => new global::RedBolt.Sample.Urho2DIsometricDemo.Urho2DIsometricDemo(),
        "RedBolt.Sample.Urho2DPlatformer.Urho2DPlatformer" => new global::RedBolt.Sample.Urho2DPlatformer.Urho2DPlatformer(),
        _ => throw new System.InvalidOperationException($"Unknown RedBolt sample application: {applicationTypeName}"),
    };
}
