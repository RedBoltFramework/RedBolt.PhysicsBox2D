# RedBolt.PhysicsBox2D workspace instructions

This repository is intentional RedBolt development. It contains the optional Box2D physics module extracted from the sibling RedBolt repository.

- RedBolt is expected at `../RedBolt` unless `RedBoltRoot` is supplied.
- RedBolt.2D is expected at `../RedBolt.2D` unless `RedBolt2DRoot` is supplied.
- Tests remain in `../RedBolt.Tests`.
- Preserve the public `RedBolt.PhysicsBox2D` and `RedBolt.Bindings.Box2D` assemblies and namespaces.
- Use generated SharpCpp bindings for Box2D. Do not add manual native bindings.
- Keep desktop, WebAssembly, and Android builds working.
- Follow the monorepo `doc/VERIFICATION.md`: build affected projects and run focused tests; validate affected platform samples and packages when their behavior or integration changes. Run the full supported matrix for release validation or when requested.
