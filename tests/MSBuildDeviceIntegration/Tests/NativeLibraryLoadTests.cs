using System;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Category ("UsesDevice")]
public class NativeLibraryLoadTests : DeviceTest
{
	[Test]
	public void UnknownSystemLibraryLoadsWithoutMainThreadDispatch ()
	{
		if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
			return;
		}

		const string completedMessage = "UNKNOWN_SYSTEM_LIBRARY_LOAD_COMPLETED";
		const string timeoutMessage = "UNKNOWN_SYSTEM_LIBRARY_LOAD_TIMED_OUT";
		string packageName = PackageUtils.MakePackageName (AndroidRuntime.CoreCLR, "unknownsystemlibrary");
		var proj = new XamarinAndroidApplicationProject (packageName: packageName) {
			EmbedAssembliesIntoApk = true,
			ProjectName = "UnknownSystemLibrary",
		};
		proj.SetRuntime (AndroidRuntime.CoreCLR);
		proj.SetRuntimeIdentifiers ([DeviceAbi]);
		proj.SetDefaultTargetDevice ();
		proj.MainActivity = proj.DefaultMainActivity
			.Replace ("//${USINGS}", "using System;\nusing System.Runtime.InteropServices;\nusing System.Threading.Tasks;")
			.Replace (
				"//${AFTER_ONCREATE}",
				$$"""
			// Blocking the main thread here verifies CoreCLR directly loads libraries missing from the
			// build-time DSO cache instead of dispatching System.loadLibrary to the main thread.
			var loadTask = Task.Run (() => {
				System.Threading.Thread.Sleep (TimeSpan.FromMilliseconds (250));
				return NativeMethods.GetError ();
			});
			if (!loadTask.Wait (TimeSpan.FromSeconds (2))) {
				Android.Util.Log.Error ("NativeLibraryLoadTest", "{{timeoutMessage}}");
				return;
			}
			Android.Util.Log.Info ("NativeLibraryLoadTest", "{{completedMessage}}");
"""
			)
			.Replace (
				"//${AFTER_MAINACTIVITY}",
				"""
	static class NativeMethods
	{
		[DllImport ("libGLESv2", EntryPoint = "glGetError")]
		public static extern uint GetError ();
	}
"""
			);

		using var builder = CreateApkBuilder (packageName: packageName);
		Assert.IsTrue (builder.Install (proj), "Project should have installed.");

		ClearAdbLogcat ();
		bool loadTimedOut = false;
		bool appCompleted = MonitorAdbLogcat (
			line => {
				loadTimedOut |= line.Contains (timeoutMessage, StringComparison.Ordinal);
				return loadTimedOut || line.Contains (completedMessage, StringComparison.Ordinal);
			},
			Path.Combine (Root, builder.ProjectDirectory, "unknown-system-library-load.log"),
			timeout: 30,
			onMonitoringStarted: () => StartActivityAndAssert (proj)
		);

		Assert.IsFalse (loadTimedOut, "The worker P/Invoke waited for the blocked Android main thread.");
		Assert.IsTrue (appCompleted, $"Output did not contain {completedMessage}.");
	}

	[Test]
	public void MissingNativeLibraryHasUsefulErrorMessage ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		bool isRelease = runtime == AndroidRuntime.NativeAOT;
		if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
			return;
		}

		var proj = new XamarinAndroidApplicationProject (
			packageName: PackageUtils.MakePackageName (runtime, "missingnativelibrary")
		) {
			IsRelease = isRelease,
			ProjectName = "MissingNativeLibrary",
		};
		proj.SetRuntime (runtime);
		proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
		proj.SetDefaultTargetDevice ();

		string libraryName;
		string removeLibraryItems;
		if (runtime == AndroidRuntime.NativeAOT) {
			libraryName = $"lib{proj.ProjectName}.so";
			removeLibraryItems = $@"
			<FrameworkNativeLibrary Remove=""@(FrameworkNativeLibrary)""
				Condition="" '%(FrameworkNativeLibrary.ArchiveFileName)' == '{libraryName}' or '%(FrameworkNativeLibrary.FileName)%(FrameworkNativeLibrary.Extension)' == '{libraryName}' "" />
			<_ApplicationSharedLibrary Remove=""@(_ApplicationSharedLibrary)""
				Condition="" '%(_ApplicationSharedLibrary.ArchiveFileName)' == '{libraryName}' or '%(_ApplicationSharedLibrary.FileName)%(_ApplicationSharedLibrary.Extension)' == '{libraryName}' "" />";
		} else {
			libraryName = "libmonodroid.so";
			removeLibraryItems = @"
			<FrameworkNativeLibrary Remove=""@(FrameworkNativeLibrary)""
				Condition="" '%(FrameworkNativeLibrary.ArchiveFileName)' == 'libmonodroid.so' or '%(FrameworkNativeLibrary.FileName)%(FrameworkNativeLibrary.Extension)' == 'libmonodroid.so' "" />
			<_ApplicationSharedLibrary Remove=""@(_ApplicationSharedLibrary)""
				Condition="" '%(_ApplicationSharedLibrary.ArchiveFileName)' == 'libmonodroid.so' or '%(_ApplicationSharedLibrary.FileName)%(_ApplicationSharedLibrary.Extension)' == 'libmonodroid.so' "" />";
		}

		proj.Imports.Add (new Import (() => "Directory.Build.targets") {
			TextContent = () => $"""
<Project>
	<Target Name="_RemoveNativeLibraryForTest" BeforeTargets="_BuildApkEmbed">
		<ItemGroup>
			{removeLibraryItems}
		</ItemGroup>
	</Target>
</Project>
"""
		});

		using var builder = CreateApkBuilder ();
		Assert.IsTrue (builder.Install (proj), "Project should have installed.");

		string outputDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
		string[] apks = Directory.GetFiles (outputDirectory, $"{proj.PackageName}-Signed.apk", SearchOption.AllDirectories);
		Assert.IsNotEmpty (apks, "The signed APK should exist.");
		using (var apk = ZipHelper.OpenZip (apks [0])) {
			Assert.IsFalse (apk.ContainsEntry ($"lib/{DeviceAbi}/{libraryName}"),
				$"{libraryName} should have been removed from the APK.");
		}

		var expectedMessages = new HashSet<string> {
			$"Failed to load native library '{libraryName}'.",
			"Supported ABIs:",
			"Native library directory:",
			"library exists: false",
			"APKs:",
			"contains no matching native libraries",
			"The application installation may be corrupt; reinstalling the application may fix this error.",
		};
		ClearAdbLogcat ();
		string logcatPath = Path.Combine (Root, builder.ProjectDirectory, "native-library-load.log");
		bool foundDiagnostic = MonitorAdbLogcat (
			line => {
				expectedMessages.RemoveWhere (message => line.Contains (message, StringComparison.Ordinal));
				return expectedMessages.Count == 0;
			},
			logcatPath,
			timeout: 45,
			onMonitoringStarted: () => AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity")
		);

		Assert.IsTrue (foundDiagnostic,
			$"The native library diagnostic was incomplete. Missing: {string.Join (", ", expectedMessages)}");
	}
}
