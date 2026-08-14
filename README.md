# RedBolt.PhysicsBox2D

Box2D 3 physics integration for [RedBolt](https://github.com/RedBoltFramework/RedBolt), built on the sibling [RedBolt.2D](https://github.com/RedBoltFramework/RedBolt.2D) rendering module.

The source build expects RedBolt in `../RedBolt` and RedBolt.2D in `../RedBolt.2D` by default. Override `RedBoltRoot` or `RedBolt2DRoot` when the repositories use a different layout.

```sh
dotnet build RedBolt.PhysicsBox2D.slnx --configuration Debug
```

Desktop, WebAssembly, and Android samples are included under `src/RedBolt/Sample`.

Tests remain in the sibling `RedBolt.Tests` repository. See [building](doc/BUILDING.md), [package production](packaging/nuget/README.md), and the [physics-specific deviations](doc/ALLOWED_DEVIATIONS.md).
