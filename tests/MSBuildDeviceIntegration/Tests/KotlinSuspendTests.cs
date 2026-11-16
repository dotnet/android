using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class KotlinSuspendTests : DeviceTest
	{
		// Binds a Gradle module with Kotlin `suspend` functions and awaits the generated `*Async` methods.
		[Test]
		public void SuspendFunctionsAsTasks ([Values (false, true)] bool isRelease)
		{
			const string Expected = "KotlinSuspend: sum=5 greeting=Hello, Kotlin nothing=True failure=boom canceled=True";

			var gradleDir = Path.Combine (Root, "temp", "gradle", TestName);
			if (Directory.Exists (gradleDir))
				Directory.Delete (gradleDir, recursive: true);

			var module = new AndroidGradleModule (Path.Combine (gradleDir, "SuspendModule")) {
				PackageName = "com.example.suspendtest",
				JavaSources = {
					new AndroidItem.AndroidJavaSource ("SuspendFunctions.kt") {
						TextContent = () => """
package com.example.suspendtest

import kotlin.coroutines.resume
import kotlin.coroutines.suspendCoroutine
import kotlinx.coroutines.delay

class SuspendFunctions {
    suspend fun add(a: Int, b: Int): Int = suspendCoroutine { c -> Thread { c.resume(a + b) }.start() }
    suspend fun greet(name: String): String = "Hello, $name"
    suspend fun nothing(): String? = null
    suspend fun fail(message: String): Int = suspendCoroutine { c ->
        Thread { c.resumeWith(Result.failure(IllegalStateException(message))) }.start()
    }
    suspend fun waitForCancel() = delay(Long.MAX_VALUE)
}
""",
					},
				},
			};
			module.BuildGradleFileContent = $$"""
plugins {
    id("com.android.library")
}
android {
    namespace = "com.example.suspendtest"
    compileSdk = {{module.CompileSdk}}
    defaultConfig {
        minSdk = {{module.MinSdk}}
    }
}
dependencies {
    compileOnly("org.jetbrains.kotlinx:kotlinx-coroutines-core:1.10.2")
}
""";
			var gradleProject = new AndroidGradleProject (gradleDir) {
				Modules = { module },
			};
			gradleProject.Create ();

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				PackageReferences = {
					new Package { Id = "Xamarin.KotlinX.Coroutines.Core", Version = "1.11.0.1" },
				},
				OtherBuildItems = {
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", module.Name },
							{ "Configuration", "Release" },
							{ "Bind", "true" },
						},
					},
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", """
			RunKotlin ();
		}

		async void RunKotlin ()
		{
			try {
				var kotlin = new Com.Example.Suspendtest.SuspendFunctions ();
				int sum = await kotlin.AddAsync (2, 3);
				string greeting = await kotlin.GreetAsync ("Kotlin");
				string nothing = await kotlin.NothingAsync ();
				string failure = "";
				try {
					await kotlin.FailAsync ("boom");
				} catch (Java.Lang.IllegalStateException e) {
					failure = e.Message;
				}
				bool canceled = false;
				using var cts = new System.Threading.CancellationTokenSource (100);
				try {
					await kotlin.WaitForCancelAsync (cts.Token);
				} catch (System.OperationCanceledException) {
					canceled = true;
				}
				Android.Util.Log.Info ("KotlinSuspend", $"KotlinSuspend: sum={sum} greeting={greeting} nothing={nothing == null} failure={failure} canceled={canceled}");
			} catch (System.Exception e) {
				Android.Util.Log.Error ("KotlinSuspend", $"KotlinSuspend: FAILED {e}");
			}
""");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			ClearAdbLogcat ();
			RunProjectAndAssert (proj, builder);
			string failed = null;
			Assert.IsTrue (MonitorAdbLogcat (line => {
				if (line.Contains ("KotlinSuspend: FAILED"))
					failed = line;
				return line.Contains (Expected) || failed != null;
			}, Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 60), $"Output did not contain '{Expected}'.");
			Assert.IsNull (failed);
		}
	}
}
