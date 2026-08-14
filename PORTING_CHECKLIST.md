# PhysicsBox2D porting checklist

This inventory moved from the base RedBolt repository when the optional 2D modules were extracted.

## B5. Physics2D `OPTIONAL` — Box2D; depends on Scene, Graphics

- [ ] `Physics2D.h`
  - [ ] registration entry point
- [ ] `PhysicsEvents2D.h`
  - [ ] 2D physics event definitions
- [ ] `PhysicsUtils2D.h`
  - [ ] Box2D <-> engine conversion helpers
- [ ] `PhysicsWorld2D.h`
  - [ ] `struct PhysicsRaycastResult2D`
  - [ ] `PhysicsWorld2D : Component, b2ContactListener, b2Draw`
- [ ] `RigidBody2D.h`
  - [ ] `RigidBody2D : Component`
- [ ] `CollisionShape2D.h`
  - [ ] `CollisionShape2D : Component`
- [ ] `CollisionBox2D.h`
  - [ ] `CollisionBox2D : CollisionShape2D`
- [ ] `CollisionCircle2D.h`
  - [ ] `CollisionCircle2D : CollisionShape2D`
- [ ] `CollisionPolygon2D.h`
  - [ ] `CollisionPolygon2D : CollisionShape2D`
- [ ] `CollisionChain2D.h`
  - [ ] `CollisionChain2D : CollisionShape2D`
- [ ] `CollisionEdge2D.h`
  - [ ] `CollisionEdge2D : CollisionShape2D`
- [ ] `Constraint2D.h`
  - [ ] `Constraint2D : Component`
- [ ] `ConstraintDistance2D.h`
  - [ ] `ConstraintDistance2D : Constraint2D`
- [ ] `ConstraintFriction2D.h`
  - [ ] `ConstraintFriction2D : Constraint2D`
- [ ] `ConstraintGear2D.h`
  - [ ] `ConstraintGear2D : Constraint2D`
- [ ] `ConstraintMotor2D.h`
  - [ ] `ConstraintMotor2D : Constraint2D`
- [ ] `ConstraintMouse2D.h`
  - [ ] `ConstraintMouse2D : Constraint2D`
- [ ] `ConstraintPrismatic2D.h`
  - [ ] `ConstraintPrismatic2D : Constraint2D`
- [ ] `ConstraintPulley2D.h`
  - [ ] `ConstraintPulley2D : Constraint2D`
- [ ] `ConstraintRevolute2D.h`
  - [ ] `ConstraintRevolute2D : Constraint2D`
- [ ] `ConstraintRope2D.h`
  - [ ] `ConstraintRope2D : Constraint2D`
- [ ] `ConstraintWeld2D.h`
  - [ ] `ConstraintWeld2D : Constraint2D`
- [ ] `ConstraintWheel2D.h`
  - [ ] `ConstraintWheel2D : Constraint2D`
