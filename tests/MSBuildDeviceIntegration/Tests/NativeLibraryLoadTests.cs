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
