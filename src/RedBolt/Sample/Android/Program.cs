namespace RedBolt.Sample.Android;

using System;
using System.Linq;

using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.OS;
using global::Android.Widget;

using RedBolt.IO;

[Activity(Label = "RedBolt samples", MainLauncher = true, Exported = true)]
public sealed class SamplePickerActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var requestedSample = Intent?.GetStringExtra(SampleActivity.SampleExtra);
        if (requestedSample is not null)
        {
            Launch(requestedSample);
            return;
        }

        var list = new ListView(this)
        {
            Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItem1,
                SampleCatalog.Entries.Select(static entry => entry.Name).ToArray())
        };
        list.ItemClick += (_, eventArgs) => Launch(SampleCatalog.Entries[eventArgs.Position].Id);
        SetContentView(list);
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (Intent?.GetStringExtra(SampleActivity.SampleExtra) is not null)
            Finish();
    }

    private void Launch(string sampleId)
    {
        var intent = new Intent(this, typeof(SampleActivity));
        intent.PutExtra(SampleActivity.SampleExtra, sampleId);
        StartActivity(intent);
    }
}

[Activity(Label = "RedBolt samples", Exported = false,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.KeyboardHidden | ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class SampleActivity : Org.Libsdl.App.SDLActivity
{
    internal const string SampleExtra = "sample";
    internal const string ScreenshotExtra = "screenshot";

    protected override void RunMain()
    {
        var sampleId = Intent?.GetStringExtra(SampleExtra)
            ?? throw new InvalidOperationException("A RedBolt sample id is required.");
        var sample = SampleCatalog.Entries.FirstOrDefault(entry => entry.Id == sampleId)
            ?? throw new InvalidOperationException($"Unknown RedBolt sample: {sampleId}");

        global::Android.Util.Log.Info("RedBolt", $"Starting sample {sample.Id}");
        VerifyPackagedAssets();
        var applicationType = typeof(SampleActivity).Assembly.GetType(sample.ApplicationType, true)!;
        using var application = (RedBolt.Engine.Application?)Activator.CreateInstance(applicationType, true)
            ?? throw new InvalidOperationException($"Cannot create RedBolt sample application: {sample.ApplicationType}");

        try
        {
            var exitCode = application.Run([]);
            global::Android.Util.Log.Info("RedBolt", $"Sample {sample.Id} exited with code {exitCode}");
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Error("RedBolt", exception.ToString());
        }
    }

    private static void VerifyPackagedAssets()
    {
        using var assets = new MountedDirectory("/apk/CoreData");
        using var plane = assets.OpenFile(new FileIdentifier("Models/Plane.mdl"), RedBolt.IO.FileMode.Read)
            ?? throw new InvalidOperationException("Packaged CoreData assets are unavailable.");
        global::Android.Util.Log.Info("RedBolt", $"Verified packaged CoreData asset ({plane.Size} bytes)");
    }
}

internal sealed record SampleEntry(string Id, string Name, string ApplicationType);

internal static class SampleCatalog
{
    internal static readonly SampleEntry[] Entries =
    [
        new("physics2d", "Physics2D", "RedBolt.Sample.Physics2D.Physics2D"),
        new("physics2d-rope", "Physics2D Rope", "RedBolt.Sample.Physics2DRope.Physics2DRope"),
        new("physics2d-constraints", "Physics2D Constraints", "RedBolt.Sample.Physics2DConstraints.Physics2DConstraints"),
        new("urho2d-isometric-demo", "Urho2D Isometric Demo", "RedBolt.Sample.Urho2DIsometricDemo.Urho2DIsometricDemo"),
        new("urho2d-platformer", "Urho2D Platformer", "RedBolt.Sample.Urho2DPlatformer.Urho2DPlatformer"),
    ];
}
