#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
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

			var packageManagerTask = new GeneratePackageManagerJava {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				MainAssembly = "linked/HelloAndroid.dll",
				OutputDirectory = Path.Combine (path, "src", "mono"),
			};

			var configTask = new GenerateJavaApplicationConfig {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				OutputFile = Path.Combine (path, "src", "net", "dot", "android", "AppBootstrapConfig.java"),
				AndroidPackageName = "com.microsoft.net6.helloandroid",
				Environments = new ITaskItem [] { new TaskItem (Path.Combine (path, "myenv.txt")) },
			};

			Assert.IsTrue (packageManagerTask.Execute (), "GeneratePackageManagerJava task should have executed.");
			Assert.IsTrue (configTask.Execute (), "GenerateJavaApplicationConfig task should have executed.");

			AssertFileContentsMatch (Path.Combine (XABuildPaths.TestAssemblyOutputDirectory, "Expected", "CheckPackageManagerAssemblyOrder.java"), Path.Combine(path, "src", "mono", "MonoPackageManager_Resources.java"));
			var txt = File.ReadAllText (configTask.OutputFile);
			CollectionAssert.Contains (JavaAppConfigTestHelper.Read (txt).Strings, "YYYY", "Java bootstrap should contain the environment value.");

			File.WriteAllText (Path.Combine (path, "myenv.txt"), @"MYENV=XXXX");
			Assert.IsTrue (configTask.Execute (), "GenerateJavaApplicationConfig task should have executed. (run 2)");
			txt = File.ReadAllText (configTask.OutputFile);
			CollectionAssert.Contains (JavaAppConfigTestHelper.Read (txt).Strings, "XXXX", "Java bootstrap should contain the updated environment value.");
		}

		[Test]
		public void GenerateBootstrapAndNativeFormatMarkerWithoutAssemblyStoreState ()
		{
			var path = Path.Combine (Root, "temp", nameof (GenerateBootstrapAndNativeFormatMarkerWithoutAssemblyStoreState));
			Directory.CreateDirectory (path);

			File.WriteAllText (Path.Combine (path, "myenv.txt"), @"MYENV=ZZZZ");

			var configTask = new GenerateJavaApplicationConfig {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				OutputFile = Path.Combine (path, "src", "net", "dot", "android", "AppBootstrapConfig.java"),
				AndroidPackageName = "com.microsoft.net6.helloandroid",
				Environments = [new TaskItem (Path.Combine (path, "myenv.txt"))],
			};
			var formatTask = new GenerateNativeApplicationConfigSources {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				EnvironmentOutputDirectory = Path.Combine (path, "env"),
				SupportedAbis = ["arm64-v8a"],
			};

			Assert.IsTrue (configTask.Execute (), "GenerateJavaApplicationConfig task should have executed.");
			Assert.IsTrue (formatTask.Execute (), "GenerateNativeApplicationConfigSources task should have executed.");

			var txt = File.ReadAllText (configTask.OutputFile);
			CollectionAssert.Contains (JavaAppConfigTestHelper.Read (txt).Strings, "ZZZZ", "Java bootstrap should contain the custom environment value.");
			txt = File.ReadAllText (Path.Combine (formatTask.EnvironmentOutputDirectory, "environment.arm64-v8a.ll"));
			StringAssert.Contains ("format_tag", txt);
			StringAssert.DoesNotContain ("AssemblyStore", txt, "Assembly-store state must remain runtime-owned.");
		}
	}
}
