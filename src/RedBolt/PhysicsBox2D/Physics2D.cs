// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Core;

/// <summary>Entry point for the 2D physics library.</summary>
public static class Physics2D
{
    /// <summary>Register 2D physics library objects.</summary>
    public static void RegisterPhysics2DLibrary(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RegisterAssembly(typeof(Physics2D).Assembly);
    }
}
