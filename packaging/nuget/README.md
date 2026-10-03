# RedBolt.PhysicsBox2D NuGet packages

RedBolt.PhysicsBox2D versions independently from both dependencies, but every package records exact RedBolt and RedBolt.2D package versions. Produce the matching feeds first in `<build root>/package/nuget/RedBolt/out` (`<build root>` is the monorepo `build/` inside the monorepo and the repository's own `build/` standalone; `REDBOLT_BUILD_ROOT` overrides it) and `<build root>/package/nuget/RedBolt.2D/out`.

Create Linux x64 packages from Release outputs, building the add-on first:

```sh
dotnet run --file packaging/nuget/package.cs -- create --rids linux-x64
```

Use `--no-build` when all selected outputs already exist. Select additional prebuilt targets with a comma-separated list such as `--rids linux-x64,android-x64,android-arm64,browser-wasm`. Override dependency version discovery only for an intentional local feed with `--redbolt-version VERSION` or `--redbolt-2d-version VERSION`.

The command writes independently versioned packages below `<build root>/package/nuget/RedBolt.PhysicsBox2D/out`:

- `RedBolt.PhysicsBox2D`
- `RedBolt.PhysicsBox2D.Runtime.<rid>`

The runtime facade brings in the managed physics package, the exact sibling `RedBolt.2D` package set, the matching `RedBolt.Core.Runtime.<rid>` package, the Box2D binding assembly, and `RedBolt.Native.Box2D` for the selected RID.

Validate package contents, exact dependency versions, and a package-only consumer with:

```sh
dotnet run --file packaging/nuget/package.cs -- validate --rids linux-x64
```
