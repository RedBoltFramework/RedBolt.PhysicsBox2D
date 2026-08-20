# Building RedBolt.PhysicsBox2D

Keep this repository beside RedBolt, RedBolt.2D, and RedBolt.Tests:

```text
RedBolt/
RedBolt.2D/
RedBolt.PhysicsBox2D/
RedBolt.Tests/
```

Build the Linux x64 modules and native Box2D wrapper:

```sh
dotnet build src/RedBolt/Native/Box2D/RedBolt.Bindings.Box2D.csproj --configuration Debug
dotnet build src/RedBolt/PhysicsBox2D/RedBolt.PhysicsBox2D.csproj --configuration Debug
```

The solution accepts the same platform, architecture, module, and sample selection properties as RedBolt. Its saved selection is independent and is written below `build/BuildConfig.props`.

Build the aggregate WebAssembly or Android sample hosts directly:

```sh
dotnet build src/RedBolt/Sample/Web/RedBolt.Sample.Web.csproj --configuration Release -p:RedBoltEnablePlatforms=web
dotnet build src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj --configuration Debug -f net10.0-android -p:RedBoltEnablePlatforms=android -p:RedBoltEnableArchitectures=all
```

Generated bindings, native libraries, managed assemblies, sample outputs, and packages are written below this repository's `build/` directory. Base RedBolt and 2D rendering outputs remain below their sibling repositories.
