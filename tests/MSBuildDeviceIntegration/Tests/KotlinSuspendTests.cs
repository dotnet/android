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
		// R8 checks the keep rules for the Kotlin members that KotlinCoroutines finds by name.
		[Test]
		public void SuspendFunctionsAsTasks ()
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
    @Volatile var stopped = false
        private set
    suspend fun waitForCancel() {
        try {
            delay(Long.MAX_VALUE)
        } finally {
            stopped = true
        }
    }
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
				IsRelease = true,
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
			proj.SetProperty ("AndroidLinkTool", "r8");
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
				// Cancel after the coroutine suspends, and check that the Kotlin code stops too.
				bool canceled = false;
				using var cts = new System.Threading.CancellationTokenSource ();
				var wait = kotlin.WaitForCancelAsync (cts.Token);
				cts.Cancel ();
				try {
					await wait;
				} catch (System.OperationCanceledException) {
					for (int i = 0; i < 50 && !kotlin.Stopped; i++)
						await System.Threading.Tasks.Task.Delay (100);
					canceled = kotlin.Stopped;
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
