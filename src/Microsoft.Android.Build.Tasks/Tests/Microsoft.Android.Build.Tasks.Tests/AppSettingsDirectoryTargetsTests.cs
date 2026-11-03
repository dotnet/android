using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class AppSettingsDirectoryTargetsTests : BaseTest
{
	[Test]
	public void ResolvesSettingsAndKeystore (
		[Values ("default", "project", "project-trailing-slash", "dependency", "global", "global-empty")] string settingsSource,
		[Values] bool overrideKeystore, [Values] bool multithreaded)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		var source = XDocument.Load (Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
		var target = new XElement (source.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_ResolveMonoAndroidSdks"));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		// Isolate SDK-path resolution from the unrelated API-level constants task.
		target.Elements ("GetAndroidDefineConstants").Single ().Remove ();
		Assert.IsFalse (source.Descendants ().Any (element =>
			element.Name.LocalName == "UsingTask" &&
			(string?) element.Attribute ("TaskName") == "Xamarin.Android.Tasks.GetAppSettingsDirectory"));

		string customSettings = Path.Combine ("relative settings", "custom");
		if (settingsSource == "project-trailing-slash") {
			customSettings += Path.DirectorySeparatorChar;
		}
		string customKeystore = Path.Combine ("custom keys", "debug.keystore");
		var properties = new XElement ("PropertyGroup",
			new XElement ("MonoAndroidToolsDirectory", directory),
			new XElement ("_AndroidSdkDirectory", directory),
			new XElement ("_JavaSdkDirectory", directory),
			new XElement ("AndroidEnableMultiDex", "false"),
			new XElement ("_ResolveMonoAndroidSdksDependsOn", "_SetSettingsOverride"));
		if (settingsSource.StartsWith ("project", StringComparison.Ordinal)) {
			properties.Add (new XElement ("AppSettingsDirectory", customSettings));
		}
		if (overrideKeystore) {
			properties.Add (new XElement ("_ApkDebugKeyStore", customKeystore));
		}
		var dependency = new XElement ("Target", new XAttribute ("Name", "_SetSettingsOverride"));
		if (settingsSource == "dependency") {
			dependency.Add (new XElement ("PropertyGroup", new XElement ("AppSettingsDirectory", customSettings)));
		}
		string project = Path.Combine (directory, "settings.proj");
		new XDocument (new XElement ("Project", properties, dependency, target,
			new XElement ("Target", new XAttribute ("Name", "_CaptureBeforeResolution"),
				new XElement ("PropertyGroup", new XElement ("SettingsBeforeResolution", "$(AppSettingsDirectory)")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		string globalSettings = settingsSource == "global" ? customSettings : "";
		string [] arguments = [
			"msbuild", project,
			"-t:_CaptureBeforeResolution;_ResolveMonoAndroidSdks",
			"-getProperty:AppSettingsDirectory,_AppSettingsDirectory,_ApkDebugKeyStore,SettingsBeforeResolution",
			"-v:quiet", "-nr:false",
			.. multithreaded ? new [] { "-m", "-mt", "-warnaserror:MSB4286,MSB4287,MSB4288" } : [],
			.. settingsSource.StartsWith ("global", StringComparison.Ordinal) ? new [] { "-p:AppSettingsDirectory=" + globalSettings } : [],
		];
		var result = NativeAotBootstrapTestTools.Run (dotnet, arguments);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		using var document = JsonDocument.Parse (result.Output);
		var actual = document.RootElement.GetProperty ("Properties");
		string expectedSettings = settingsSource switch {
			"default" or "global-empty" => Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin", "Mono for Android"),
			_ => customSettings,
		};
		string expectedDirectory = expectedSettings.Length == 0 || Path.EndsInDirectorySeparator (expectedSettings)
			? expectedSettings : expectedSettings + Path.DirectorySeparatorChar;
		Assert.AreEqual (expectedSettings, actual.GetProperty ("AppSettingsDirectory").GetString ());
		Assert.AreEqual (expectedDirectory, actual.GetProperty ("_AppSettingsDirectory").GetString ());
		Assert.AreEqual (overrideKeystore ? customKeystore : expectedDirectory + "debug.keystore",
			actual.GetProperty ("_ApkDebugKeyStore").GetString ());
		Assert.AreEqual (settingsSource == "default" || settingsSource == "dependency" ? "" :
			settingsSource.StartsWith ("global", StringComparison.Ordinal) ? globalSettings : customSettings,
			actual.GetProperty ("SettingsBeforeResolution").GetString (),
			"The default must not be assigned during project evaluation or before SDK resolution.");
	}
}
