using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Category ("UsesDevice")]
[Category ("MayHang")]
public class MarshalMethodsGCHangTests : DeviceTest
{
	static readonly string MarshalMethodsAppRuns_PermissionManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{0}"">
        <uses-sdk />
        <application android:label=""{0}"">
        </application>
        <uses-permission android:name=""android.permission.INTERNET"" />
</manifest>";

	static readonly string MarshalMethodsAppRuns_MainActivity = @"using Android.Media;

namespace marshal2;

[Activity (Label = ""@string/app_name"", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		SetContentView (Resource.Layout.Main);
	}

	protected override void OnStart ()
	{
		base.OnStart ();

		try {
			var mp = new MediaPlayer ();
			mp.SetDataSource (new StreamMediaDataSource (new MemoryStream (new byte[65536])));
			mp.Prepare ();
		} catch (Java.IO.IOException) {
			GC.Collect ();
		}
	}

	class StreamMediaDataSource (System.IO.Stream data) : MediaDataSource
	{
		public override long Size => data.Length;

		public override int ReadAt (long position, byte[]? buffer, int offset, int size)
		{
			try {
				Console.WriteLine ($""XXX:START StreamMediaDataSource.ReadAt {position} {buffer} {buffer?.Length ?? 0} {offset} {size}"");

				// Allocate enough to trigger GC
				for (int i = 0; i < 1000; i++) {
					_ = new byte[8192];
				}

				if (data.CanSeek) {
					data.Seek (position, SeekOrigin.Begin);
				}
				return data.Read (buffer ?? [], offset, size);
			} finally {
				Console.WriteLine ($""XXX:END //StreamMediaDataSource.ReadAt {position} {buffer} {buffer?.Length ?? 0} {offset} {size}"");
			}
		}

		public override void Close ()
		{
			data.Dispose ();
			data = System.IO.Stream.Null;
		}
	}
}
";

	// All Tests here require the emulator to be started with -writable-system
	[Test]
	public void MarshalMethodsAppRuns ()
	{
		var proj = new XamarinAndroidApplicationProject (packageName: "marshal2") {
			IsRelease = true,
			EnableMarshalMethods = true,
			TargetFramework = "net9.0-android",
			SupportedOSPlatformVersion = "23",
			TrimModeRelease = TrimMode.Full,
			ProjectName = "marshal2",
		};

		proj.SetAndroidSupportedAbis (DeviceAbi);
		proj.AndroidManifest = String.Format (MarshalMethodsAppRuns_PermissionManifest, proj.PackageName);
		proj.MainActivity = MarshalMethodsAppRuns_MainActivity;
		proj.SetDefaultTargetDevice ();

		using var apkBuilder = CreateApkBuilder (Path.Combine ("temp", TestName));
		Assert.True (apkBuilder.Install (proj), "Project should have installed.");
		RunProjectAndAssert (proj, apkBuilder);

		const string expectedLogcatOutput = "XXX:END //StreamMediaDataSource.ReadAt";
		Assert.IsTrue (
			MonitorAdbLogcat (
				InstallAndRunTests.CreateLineChecker (expectedLogcatOutput),
				logcatFilePath: Path.Combine (Root, apkBuilder.ProjectDirectory, "startup-logcat.log"), timeout: 60
			),
			$"Output did not contain {expectedLogcatOutput}!"
		);
	}
}
