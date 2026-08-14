# RedBolt.PhysicsBox2D workspace instructions

This repository is intentional RedBolt development. It contains the optional Box2D physics module extracted from the sibling RedBolt repository.

- RedBolt is expected at `../RedBolt` unless `RedBoltRoot` is supplied.
- RedBolt.2D is expected at `../RedBolt.2D` unless `RedBolt2DRoot` is supplied.
- Tests remain in `../RedBolt.Tests`.
- Preserve the public `RedBolt.PhysicsBox2D` and `RedBolt.Bindings.Box2D` assemblies and namespaces.
- Use generated SharpCpp bindings for Box2D. Do not add manual native bindings.
- Keep desktop, WebAssembly, and Android builds working.
- Build the smallest affected project and run the focused PhysicsBox2D tests after implementation changes.
- At completion, build the solution, build Web and Android samples, create packages, run focused tests, and review a captured sample screenshot.
