using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Category ("UsesDevice")]
public class BootstrapConfigurationTests : DeviceTest
{
	[TestCase (false, 4097)]
	[TestCase (false, 65537)]
	[TestCase (true, 4097)]
	[TestCase (true, 65537)]
	public void LargeBundledSystemProperties (bool isRelease, int valueBytes)
	{
		if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: isRelease)) {
			return;
		}

		const string systemProperty = "debug.dotnet.max_grefc";
		const string runtimeProperty = "Bootstrap.LargeProperty";
		const string maximumReferences = "12345";
		string systemValue = new string ('0', valueBytes - maximumReferences.Length) + maximumReferences;
		string runtimeValue = new string ('x', valueBytes) + "-\u00e9\U0001f680-end";
		string successMarker = $"LARGE_BOOTSTRAP_PROPERTIES_OK:{valueBytes}";
		var expectedMessages = new HashSet<string> (StringComparer.Ordinal) {
			$"Overriding max JNI Global Reference count to {maximumReferences}",
			successMarker,
		};
		Assert.That (Encoding.UTF8.GetByteCount (systemValue), Is.EqualTo (valueBytes));
		Assert.That (RunAdbCommand ($"shell getprop {systemProperty}").Trim (), Is.Empty,
			"An OS property must not override the bundled property under test.");

		string packageName = PackageUtils.MakePackageName (
			AndroidRuntime.CoreCLR, $"largebootstrap{valueBytes}{isRelease}");
		var proj = new XamarinAndroidApplicationProject (packageName: packageName) {
			IsRelease = isRelease,
			EmbedAssembliesIntoApk = true,
			OtherBuildItems = {
				new BuildItem ("AndroidEnvironment", "bootstrap.env") {
					// Leading zeros make the native parser consume the entire value, including its final chunk.
					TextContent = () => $"{systemProperty}={systemValue}\ndebug.dotnet.log=gc\n",
				},
				new BuildItem ("RuntimeHostConfigurationOption", runtimeProperty) {
					Metadata = {
						{ "Value", runtimeValue },
					},
				},
			},
		};
		proj.SetRuntime (AndroidRuntime.CoreCLR);
		proj.SetRuntimeIdentifiers ([DeviceAbi]);
		proj.SetDefaultTargetDevice ();
		proj.SetProperty ("PublishReadyToRun", "false");
		if (isRelease) {
			proj.SetProperty ("AndroidLinkTool", "r8");
		}
		proj.MainActivity = proj.DefaultMainActivity.Replace (
			"//${AFTER_ONCREATE}",
			$$"""
			string expected = new string ('x', {{valueBytes.ToString (CultureInfo.InvariantCulture)}}) + "-\u00e9\U0001f680-end";
			var actual = AppContext.GetData ("{{runtimeProperty}}") as string;
			if (!string.Equals (actual, expected, StringComparison.Ordinal)) {
				throw new InvalidOperationException ($"Large runtime property did not round-trip: expected {expected.Length} characters, got {actual?.Length}.");
			}
			Console.WriteLine ("{{successMarker}}");
			""");

		using var builder = CreateApkBuilder (packageName: packageName);
		Assert.IsTrue (builder.Install (proj), "The large-bootstrap application should build and install.");
		string configPath = builder.Output.GetIntermediaryPath (
			Path.Combine ("android", "src", "net", "dot", "android", "AppBootstrapConfig.java"));
		string source = File.ReadAllText (configPath);
		var config = JavaAppConfigTestHelper.Read (source);
		int systemIndex = Array.IndexOf (config.Strings, systemProperty);
		Assert.That (systemIndex, Is.GreaterThanOrEqualTo (0), "The property must be bundled, not just present in the environment file.");
		Assert.That (config.Strings [systemIndex + 1], Is.EqualTo (systemValue));
		Assert.That (config.Data.Length, Is.GreaterThan (valueBytes));
		Assert.That (source, Does.Contain ("System.arraycopy"));

		ClearAdbLogcat ();
		Assert.IsTrue (MonitorAdbLogcat (
			line => {
				expectedMessages.RemoveWhere (message => line.Contains (message, StringComparison.Ordinal));
				return expectedMessages.Count == 0;
			},
			Path.Combine (Root, builder.ProjectDirectory, "large-bootstrap-logcat.log"),
			ActivityStartTimeoutInSeconds,
			onMonitoringStarted: () => StartActivityAndAssert (proj)),
			$"Native initialization must read the complete bundled property and managed startup must verify the hosting property. Missing: {string.Join (", ", expectedMessages)}");
	}
}
