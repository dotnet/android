using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class GeneratePackageManagerJavaTests : BaseTest
	{
#pragma warning disable 414
		static object [] CheckPackageManagerAssemblyOrderChecks () => new object [] {
			new object[] {
				/* resolvedUserAssemblies */ new string [] {
					"linked/Xamarin.AndroidX.SavedState.dll",
					"linked/HelloAndroid.dll",
				},
				/* resolvedAssemblies */     new string [] {
					"linked/Xamarin.AndroidX.SavedState.dll",
					"linked/HelloAndroid.dll",
					"linked/System.Console.dll",
					"linked/System.Linq.dll",
				}
			},
			new object[] {
				/* resolvedUserAssemblies */ new string [] {
					"linked/HelloAndroid.dll",
					"linked/Xamarin.AndroidX.SavedState.dll",
				},
				/* resolvedAssemblies */     new string [] {
					"linked/Xamarin.AndroidX.SavedState.dll",
					"linked/System.Console.dll",
					"linked/System.Linq.dll",
					"linked/HelloAndroid.dll",
				}
			},
		};
#pragma warning restore 414
		[Test]
		[TestCaseSource (nameof (CheckPackageManagerAssemblyOrderChecks))]
		public void CheckPackageManagerAssemblyOrder (string[] resolvedUserAssemblies, string[] resolvedAssemblies)
		{
			// avoid a PathTooLongException because using the TestName will include ALL the arguments.
			var testHash = Files.HashString (string.Join ("", resolvedUserAssemblies) + string.Join ("", resolvedAssemblies));
			var path = Path.Combine (Root, "temp", $"CheckPackageManagerAssemblyOrder{testHash}");
			Directory.CreateDirectory (path);

			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new [] {
				new ApiInfo { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1",  Stable = true },
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0",  Stable = true },
			});
			MonoAndroidHelper.RefreshSupportedVersions (new [] {
				Path.Combine (referencePath, "MonoAndroid"),
			});

			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), $@"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='com.microsoft.net6.helloandroid' android:versionCode='1' />");
			File.WriteAllText (Path.Combine (path, "myenv.txt"), @"MYENV=YYYY");

			var metadata = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
				{"Abi", "arm64-v8a"},
			};

			var resolvedUserAssembliesList = resolvedUserAssemblies.Select (x => new TaskItem (x, metadata));
			var resolvedAssembliesList = resolvedAssemblies.Select (x => new TaskItem (x, metadata));

			var task = new GeneratePackageManagerJava {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ResolvedUserAssemblies = resolvedUserAssembliesList.ToArray (),
				ResolvedAssemblies = resolvedAssembliesList.ToArray (),
				OutputDirectory = Path.Combine(path, "src", "mono"),
				EnvironmentOutputDirectory = Path.Combine (path, "env"),
				MainAssembly = "linked/HelloAndroid.dll",
				TargetFrameworkVersion = "v6.0",
				Manifest = Path.Combine (path, "AndroidManifest.xml"),
				SupportedAbis = new string [] { "x86" , "arm64-v8a" },
				AndroidPackageName = "com.microsoft.net6.helloandroid",
				EnablePreloadAssembliesDefault = false,
				Environments = new ITaskItem [] { new TaskItem (Path.Combine (path, "myenv.txt")) },
			};
			Assert.IsTrue (task.Execute (), "Task should have executed.");
			AssertFileContentsMatch (Path.Combine (XABuildPaths.TestAssemblyOutputDirectory, "Expected", "CheckPackageManagerAssemblyOrder.java"), Path.Combine(path, "src", "mono", "MonoPackageManager_Resources.java"));
			var txt = File.ReadAllText (Path.Combine (path, "env", "environment.arm64-v8a.ll"));
			StringAssert.Contains ("YYYY", txt, "environment.arm64-v8a.ll should contain 'YYYY'");
			txt = File.ReadAllText (Path.Combine (path, "env", "environment.x86.ll"));
			StringAssert.Contains ("YYYY", txt, "environment.x86.ll should contain 'YYYY'");

			File.WriteAllText (Path.Combine (path, "myenv.txt"), @"MYENV=XXXX");
			Assert.IsTrue (task.Execute (), "Task should have executed.");
			txt = File.ReadAllText (Path.Combine (path, "env", "environment.arm64-v8a.ll"));
			StringAssert.Contains ("XXXX", txt, "environment.arm64-v8a.ll should contain 'XXXX'");
			txt = File.ReadAllText (Path.Combine (path, "env", "environment.x86.ll"));
			StringAssert.Contains ("XXXX", txt, "environment.x86.ll should contain 'XXXX'");
		}
	}
}
