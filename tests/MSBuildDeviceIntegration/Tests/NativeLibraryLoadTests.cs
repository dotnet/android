using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Category ("UsesDevice")]
public class NativeLibraryLoadTests : DeviceTest
{
	[Test]
	public void AssemblyStoreDlopenResolvesOriginalPayload ([Values] bool is64Bit, [Values] bool extractNativeLibs)
	{
		string abi = RunAdbCommand ("shell getprop ro.product.cpu.abilist").Trim ().Split (',')
			.FirstOrDefault (value => is64Bit ? value is "arm64-v8a" or "x86_64" : value == "armeabi-v7a");
		if (abi == null) {
			Assert.Ignore ($"The target does not support a {(is64Bit ? 64 : 32)}-bit CoreCLR ABI.");
			return;
		}

		string packageName = PackageUtils.MakePackageName (
			AndroidRuntime.CoreCLR, $"assemblystore{(is64Bit ? 64 : 32)}{extractNativeLibs}");
		var proj = new XamarinAndroidApplicationProject (packageName: packageName) {
			IsRelease = true,
		};
		proj.SetRuntime (AndroidRuntime.CoreCLR);
		proj.SetRuntimeIdentifiers ([abi]);
		proj.SetDefaultTargetDevice ();
		proj.SetProperty ("AdbTargetArchitecture", abi);
		proj.SetProperty ("AndroidPackageFormat", "apk");
		proj.SetProperty ("AndroidEnableAssemblyCompression", "true");
		proj.SetProperty ("PublishReadyToRun", "false");
		proj.AndroidManifest = proj.AndroidManifest.Replace (
			"<application ", $"<application android:extractNativeLibs=\"{extractNativeLibs.ToString ().ToLowerInvariant ()}\" ");
		proj.MainActivity = proj.DefaultMainActivity
			.Replace ("//${USINGS}", "using System.IO;\nusing System.Runtime.InteropServices;\nusing System.Security.Cryptography;")
			.Replace (
				"//${AFTER_ONCREATE}",
				"""
				var intent = Intent ?? throw new InvalidOperationException ("The loader test intent is missing.");
				int length = intent.GetIntExtra ("expected-store-length", 0);
				string expectedHash = intent.GetStringExtra ("expected-store-sha256")
					?? throw new InvalidOperationException ("The expected store digest is missing.");
				if (length <= 0 || Environment.Is64BitProcess != intent.GetBooleanExtra ("expected-64-bit", false)) {
					throw new InvalidOperationException ("The payload length or process ABI is incorrect.");
				}

				// Match Host::map_assembly_store_via_dlopen: RTLD_NOW | RTLD_LOCAL and the sole payload export.
				IntPtr handle = AssemblyStoreNativeMethods.dlopen ("libassembly-store.so", 2);
				if (handle == IntPtr.Zero) {
					throw new InvalidOperationException ("dlopen: " + Marshal.PtrToStringAnsi (AssemblyStoreNativeMethods.dlerror ()));
				}
				try {
					IntPtr address = AssemblyStoreNativeMethods.dlsym (handle, "_assembly_store");
					if (address == IntPtr.Zero) {
						throw new InvalidOperationException ("dlsym: " + Marshal.PtrToStringAnsi (AssemblyStoreNativeMethods.dlerror ()));
					}
					byte [] payload = new byte [length];
					Marshal.Copy (address, payload, 0, payload.Length);
					string actualHash = Convert.ToHexString (SHA256.HashData (payload));
					if (actualHash != expectedHash) {
						throw new InvalidDataException ($"Loaded assembly-store digest {actualHash} differs from {expectedHash}.");
					}
				} finally {
					if (AssemblyStoreNativeMethods.dlclose (handle) != 0) {
						throw new InvalidOperationException ("dlclose: " + Marshal.PtrToStringAnsi (AssemblyStoreNativeMethods.dlerror ()));
					}
				}
				Android.Util.Log.Info ("AssemblyStoreLoader", $"ASSEMBLY_STORE_PAYLOAD_MATCH:{expectedHash}");
				""")
			.Replace (
				"//${AFTER_MAINACTIVITY}",
				"""
				static class AssemblyStoreNativeMethods
				{
					[DllImport ("libdl")]
					public static extern IntPtr dlopen (string name, int flags);

					[DllImport ("libdl")]
					public static extern IntPtr dlsym (IntPtr handle, string name);

					[DllImport ("libdl")]
					public static extern IntPtr dlerror ();

					[DllImport ("libdl")]
					public static extern int dlclose (IntPtr handle);
				}
				""");

		using var builder = CreateApkBuilder (packageName: packageName);
		Assert.IsTrue (builder.Install (proj), "The generated-store application should build and install.");
		AssertExtractNativeLibs (builder.Output.GetIntermediaryPath (Path.Combine ("android", "AndroidManifest.xml")), extractNativeLibs);
		string rawStore = builder.Output.GetIntermediaryPath (Path.Combine ("app_shared_libraries", abi, "assembly-store.so"));
		FileAssert.Exists (rawStore);
		byte [] expected = File.ReadAllBytes (rawStore);
		Assert.Greater (expected.Length, 0);
		string expectedHash = Convert.ToHexString (SHA256.HashData (expected));
		string successMarker = $"ASSEMBLY_STORE_PAYLOAD_MATCH:{expectedHash}";

		ClearAdbLogcat ();
		Assert.IsTrue (MonitorAdbLogcat (
			line => line.Contains (successMarker, StringComparison.Ordinal),
			Path.Combine (Root, builder.ProjectDirectory, "assembly-store-loader.log"),
			ActivityStartTimeoutInSeconds,
			onMonitoringStarted: () => {
				var (code, output, error) = RunAdbCommandWithExitCode ([
					"shell", "am", "start", "-S", "-n", $"{proj.PackageName}/{proj.JavaPackageName}.MainActivity",
					"--es", "expected-store-sha256", expectedHash,
					"--ei", "expected-store-length", expected.Length.ToString (CultureInfo.InvariantCulture),
					"--ez", "expected-64-bit", is64Bit.ToString ().ToLowerInvariant (),
				]);
				Assert.AreEqual (0, code, $"The loader test activity should start: {output}\n{error}");
				StringAssert.Contains ("Starting: Intent {", output);
			}),
			"Android dlopen/dlsym must expose every byte of the generated assembly-store payload.");
	}

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
			Assert.IsNull (apk.GetEntry ($"lib/{DeviceAbi}/{libraryName}"),
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
