# Allowed deviations

These deviations apply to the Box2D physics module extracted from RedBolt:

- `PhysicsBox2D` remains an independent assembly and depends on the rendering types from sibling `RedBolt.2D`; it does not introduce an `IPhysicsWorld2D` facade or another API absent from rbfx.
- `PhysicsBox2D` uses the official Box2D 3.1.1 repository through CPM, pinned to commit `8c661469c9507d3ad6fbd2fea3f1aa71669c2fe3`, instead of rbfx's bundled Box2D 2.x. Generated SharpCpp bindings and generated native callback/query helpers are used; there is no hand-written P/Invoke layer. Box2D 3 IDs replace the old native pointers exposed by rbfx.
- Box2D 3 removed gear, pulley, friction, and rope joint types. `ConstraintGear2D` and `ConstraintPulley2D` are therefore absent; `ConstraintFriction2D` uses a zero-correction motor joint, and `ConstraintRope2D` uses a distance joint with an enabled upper limit. The constraints sample presents the nine retained or mapped constraints. `CollisionChain2D` creates connected segment shapes because the Box2D 3 chain lifetime is separate from shape IDs.
- Box2D 3 exposes a single substep count instead of separate velocity and position solver iterations, always clears accumulated forces, and removed the old contact-pair debug flag. The rbfx `VelocityIterations`, `PositionIterations`, `AutoClearForces`, `SubStepping`, and `DrawPair` serialized surface is retained for scene compatibility, but behavior follows Box2D 3 capabilities. Contact-update events use Box2D 3's pre-solve callback, so their mutable `Enabled` value still controls solver response.
