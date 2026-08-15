#:project ../../../RedBolt/src/RedBolt/ExternalModule/Packaging/RedBolt.ExternalModule.Packaging.csproj

using System.Runtime.CompilerServices;
using RedBolt.ExternalModule.Packaging;

return ExternalModulePackager.Run(args, new ExternalModulePackageManifest
{
    Name = "RedBolt.PhysicsBox2D",
    BasePackages = ["RedBolt.Core", "SharpCpp.Std", "SharpCpp.Support"],
    RequireBaseRuntime = true,
    SupportedRids = ["linux-x64", "win-x64", "android-x64", "android-arm64", "browser-wasm"],
    DefaultRids = ["linux-x64"],
    ExternalFeeds =
    [
        new(
            "redbolt-2d",
            "RedBolt.2D",
            "--redbolt-2d-version",
            "redbolt-2d-version.txt",
            ["RedBolt.2D"],
            ["RedBolt.2D"])
    ],
    BuildSteps =
    [
        new("Native/RedBolt.PhysicsBox2D.Native.csproj", BuildWhen.Linux),
        new("Native/Box2D/RedBolt.Bindings.Box2D.csproj", BuildWhen.Linux, true),
        new("PhysicsBox2D/RedBolt.PhysicsBox2D.csproj", BuildWhen.Linux, true, "-p:BuildProjectReferences=false"),
        new("PhysicsBox2D/RedBolt.PhysicsBox2D.csproj", BuildWhen.Windows, false,
            "-p:RedBoltPlatform=win-x64", "-p:RedBoltNativePreset=win-x64"),
        new("Sample/Web/RedBolt.Sample.Web.csproj", BuildWhen.Web, false, "-p:RedBoltEnablePlatforms=web"),
        new("Sample/Android/RedBolt.Sample.Android.csproj", BuildWhen.Android, false,
            "--framework", "net10.0-android", "-p:RedBoltEnablePlatforms=android", "-p:RedBoltEnableArchitectures=all")
    ],
    ManagedPackages =
    [
        new(
            "RedBolt.PhysicsBox2D",
            "Box2D 3 physics integration for RedBolt.2D.",
            [
                new("build/bin/Release/linux-x64/core/RedBolt.PhysicsBox2D.dll", "lib/net10.0/RedBolt.PhysicsBox2D.dll"),
                new("build/bin/Release/linux-x64/extern/RedBolt.Bindings.Box2D.dll", "lib/net10.0/RedBolt.Bindings.Box2D.dll")
            ],
            [
                new("RedBolt.2D", "redbolt-2d"),
                new("RedBolt.Core", PackageVersions.RedBolt),
                new("SharpCpp.Std", PackageVersions.RedBolt),
                new("SharpCpp.Support", PackageVersions.RedBolt)
            ])
    ],
    RuntimePackage = new(
        "RedBolt.PhysicsBox2D.Runtime",
        "Box2D",
        "*RedBolt.Native.Box2D*",
        "build/native/{rid}/lib/Release/wasm/RedBolt.PhysicsBox2D.Native.Web.a",
        [
            new("RedBolt.PhysicsBox2D", PackageVersions.Module),
            new("RedBolt.Core.Runtime.{rid}", PackageVersions.RedBolt)
        ]),
    Consumer = new(
        "RedBolt.PhysicsBox2D.Runtime.linux-x64",
        """
        using System;
        using RedBolt.PhysicsBox2D;
        using RedBolt.Scene2D;
        Console.WriteLine($"{typeof(PhysicsWorld2D).FullName} {typeof(StaticSprite2D).FullName}");
        """,
        OnlyWithLinuxRuntime: true)
}, SourcePath());

static string SourcePath([CallerFilePath] string path = "") => path;
