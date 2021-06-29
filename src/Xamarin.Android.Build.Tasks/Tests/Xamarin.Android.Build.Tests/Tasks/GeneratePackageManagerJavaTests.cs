using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	public class GeneratePackageManagerJavaTests : BaseTest
	{
		[Test]
		public void CheckPackageManagerAssemblyOrder ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);

			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new [] {
				new ApiInfo { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1",  Stable = true },
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0",  Stable = true },
			});
			MonoAndroidHelper.RefreshSupportedVersions (new [] {
				Path.Combine (referencePath, "MonoAndroid"),
			});

			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), $@"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='com.microsoft.net6.helloandroid' android:versionCode='1' />");

			var task = new GeneratePackageManagerJava {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ResolvedUserAssemblies = new ITaskItem []  {
					new TaskItem ("obj/Release/net6.0-android/android-arm/linked/Xamarin.AndroidX.SavedState.dll"),
					new TaskItem ("obj/Release/net6.0-android/android-arm/linked/HelloAndroid.dll"),
				},
				ResolvedAssemblies = new ITaskItem []  {
					new TaskItem ("obj/Release/net6.0-android/android-arm/linked/Xamarin.AndroidX.SavedState.dll"),
					new TaskItem ("obj/Release/net6.0-android/android-arm/linked/HelloAndroid.dll"),
					new TaskItem ("obj/Release/net6.0-android/android-arm/linked/System.Console.dll"),
					new TaskItem ("obj/Release/net6.0-android/android-arm/linked/System.Linq.dll"),
				},
				OutputDirectory = Path.Combine(path, "src", "mono"),
				EnvironmentOutputDirectory = Path.Combine (path, "env"),
				MainAssembly = "obj/Release/net6.0-android/android-arm/linked/HelloAndroid.dll",
				TargetFrameworkVersion = "v6.0",
				Manifest = Path.Combine (path, "AndroidManifest.xml"),
				IsBundledApplication = false,
				SupportedAbis = new string [] { "x86" , "arm64-v8a" },
				AndroidPackageName = "com.microsoft.net6.helloandroid",
				EnablePreloadAssembliesDefault = false,
				InstantRunEnabled = false,
			};
			Assert.IsTrue (task.Execute (), "Task should have executed.");
			AssertFileContentsMatch (Path.Combine (Root, "Expected", "CheckPackageManagerAssemblyOrder.java"), Path.Combine(path, "src", "mono", "MonoPackageManager_Resources.java"));
		}
	}
}
