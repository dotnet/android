using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class ValueTypeContainerTests : DeviceTest
	{
		const string ResultPrefix = "VALUE_TYPE_CONTAINER_RESULT";

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR)]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR)]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT)]
		public void ApplicationValueTypesInJavaContainers (string typemapImplementation, AndroidRuntime runtime)
		{
			var suffix = $"valuecontainers{typemapImplementation.Replace ("-", "")}{runtime}".ToLowerInvariant ();
			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime, suffix)) {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers ([DeviceAbi]);
			proj.SetProperty ("AndroidTypeMapImplementation", typemapImplementation);
			proj.SetProperty ("AndroidSdkDirectory", AndroidSdkResolver.GetAndroidSdkPath ());
			var javaSdkDirectory = AndroidSdkResolver.GetJavaSdkPath ();
			proj.SetProperty ("JavaSdkDirectory", javaSdkDirectory);
			proj.SetProperty ("JavaCPath", Path.Combine (javaSdkDirectory, "bin", "javac"));
			proj.SetProperty ("JarPath", Path.Combine (javaSdkDirectory, "bin", "jar"));
			proj.SetDefaultTargetDevice ();
			if (runtime == AndroidRuntime.NativeAOT) {
				proj.SetProperty ("IlcGenerateDgmlFile", "true");
			}
			proj.MainActivity = proj.ProcessSourceTemplate (ReadFixture ("MainActivity.cs"));
			proj.AndroidJavaSources.Add (new AndroidItem.AndroidJavaSource (
				Path.Combine ("java", "net", "dot", "android", "test", "ValueTypeContainerFixture.java")) {
				Encoding = Encoding.ASCII,
				TextContent = () => ReadFixture ("ValueTypeContainerFixture.java"),
				Metadata = {
					{ "Bind", bool.FalseString },
				},
			});
			proj.OtherBuildItems.Add (new AndroidItem.ProguardConfiguration ("proguard.cfg") {
				TextContent = () => ReadFixture ("proguard.cfg"),
			});

			var testDirectory = Path.Combine ("temp", $"{nameof (ApplicationValueTypesInJavaContainers)}-{typemapImplementation}-{runtime}");
			using var builder = CreateApkBuilder (testDirectory);
			try {
				Assert.IsTrue (builder.Install (proj), "The focused value-type container app should install.");
				AssertRawJavaHolderHasNoManagedBinding (builder);

				ClearAdbLogcat ();
				string resultLine = "";
				var logcatPath = Path.Combine (Root, builder.ProjectDirectory, "value-type-containers-logcat.log");
				Assert.IsTrue (
					MonitorAdbLogcat (
						line => {
							if (!line.Contains (ResultPrefix, StringComparison.Ordinal)) {
								return false;
							}
							resultLine = line;
							return true;
						},
						logcatPath,
						timeout: 90,
						onMonitoringStarted: () => StartActivityAndAssert (proj)),
					$"The focused app did not report a result. See '{logcatPath}'.");
				StringAssert.Contains ($"{ResultPrefix} PASS 9/9", resultLine);
				if (runtime == AndroidRuntime.NativeAOT) {
					var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
					var dgmlFiles = Directory.GetFiles (
						projectDirectory,
						$"{proj.ProjectName}.scan.dgml.xml",
						SearchOption.AllDirectories);
					Assert.AreEqual (1, dgmlFiles.Length, "The focused NativeAOT app should produce one scan dependency graph.");
					AssertGeneratedValueTypeRootsAreExclusive (dgmlFiles [0]);
				}
			} finally {
				RunAdbCommand ($"uninstall {proj.PackageName}");
			}
		}

		static void AssertGeneratedValueTypeRootsAreExclusive (string dgmlFile)
		{
			string loaderId = "";
			var registrationNodes = new Dictionary<string, string> (StringComparer.Ordinal);
			var incomingLinks = new Dictionary<string, List<(string Source, string Reason)>> (StringComparer.Ordinal);
			foreach (var line in File.ReadLines (dgmlFile)) {
				if (line.Contains ("<Node ", StringComparison.Ordinal)) {
					var id = GetAttribute (line, "Id");
					var label = GetAttribute (line, "Label");
					if (label == "_Microsoft_Android_TypeMaps_Microsoft_Android_Runtime_TypeMapLoader__Initialize") {
						loaderId = id;
					}
					if (label.StartsWith (
							"Mono_Android_Java_Interop_SafeJavaCollectionFactory__RegisterValueType",
							StringComparison.Ordinal) ||
							label.StartsWith (
								"__GenericDict_Mono_Android_Java_Interop_SafeJavaCollectionFactory__RegisterValueType",
								StringComparison.Ordinal)) {
						registrationNodes.Add (id, label);
					}
				} else if (line.Contains ("<Link ", StringComparison.Ordinal)) {
					var target = GetAttribute (line, "Target");
					if (!incomingLinks.TryGetValue (target, out var links)) {
						links = new List<(string Source, string Reason)> ();
						incomingLinks.Add (target, links);
					}
					links.Add ((GetAttribute (line, "Source"), GetAttribute (line, "Reason")));
				}
			}

			Assert.IsNotEmpty (loaderId, "The generated TypeMapLoader.Initialize node was not found.");
			Assert.AreEqual (
				10,
				registrationNodes.Count,
				"The eight selected shapes should emit ten call targets because each mixed dictionary has a generic dictionary and canonical method target.");
			string[] expectedRegistrationPatterns = [
				"RegisterValueTypeCollection&lt;UnnamedProject_UnnamedProject_MainActivity_AppState&gt;",
				"RegisterValueTypeDictionary&lt;S_P_CoreLib_System_Nullable_1&lt;UnnamedProject_UnnamedProject_MainActivity_AppValue&gt;__" +
					"S_P_CoreLib_System_Nullable_1&lt;UnnamedProject_UnnamedProject_MainActivity_AppState&gt;&gt;",
				"RegisterValueTypeDictionary&lt;String__UnnamedProject_UnnamedProject_MainActivity_AppState&gt;",
				"RegisterValueTypeDictionary&lt;System___Canon__UnnamedProject_UnnamedProject_MainActivity_AppState&gt;",
				"RegisterValueTypeDictionary&lt;UnnamedProject_UnnamedProject_MainActivity_AppValue__String&gt;",
				"RegisterValueTypeDictionary&lt;UnnamedProject_UnnamedProject_MainActivity_AppValue__System___Canon&gt;",
				"RegisterValueTypeDictionary&lt;UnnamedProject_UnnamedProject_MainActivity_AppValue__" +
					"UnnamedProject_UnnamedProject_MainActivity_AppState&gt;",
				"RegisterValueTypeList&lt;S_P_CoreLib_System_Nullable_1&lt;Int32&gt;&gt;",
				"RegisterValueTypeList&lt;S_P_CoreLib_System_Nullable_1&lt;UnnamedProject_UnnamedProject_MainActivity_AppState&gt;&gt;",
				"RegisterValueTypeList&lt;UnnamedProject_UnnamedProject_MainActivity_AppValue&gt;",
			];
			foreach (var pattern in expectedRegistrationPatterns) {
				Assert.IsTrue (
					ContainsValue (registrationNodes, pattern),
					$"Generated registration root '{pattern}' was not found.");
			}

			foreach (var (registrationId, label) in registrationNodes) {
				Assert.IsTrue (incomingLinks.TryGetValue (registrationId, out var links), $"Registration '{label}' has no incoming dependency.");
				Assert.AreEqual (1, links.Count, $"Registration '{label}' should have one exclusive incoming dependency.");
				Assert.AreEqual (loaderId, links [0].Source, $"Registration '{label}' should be rooted by TypeMapLoader.Initialize.");
				Assert.AreEqual ("call", links [0].Reason, $"Registration '{label}' should be reached by a direct call.");
			}
		}

		static bool ContainsValue (Dictionary<string, string> values, string pattern)
		{
			foreach (var value in values.Values) {
				if (value.Contains (pattern, StringComparison.Ordinal)) {
					return true;
				}
			}
			return false;
		}

		static string GetAttribute (string line, string name)
		{
			var prefix = $"{name}=\"";
			int start = line.IndexOf (prefix, StringComparison.Ordinal);
			if (start < 0) {
				return "";
			}
			start += prefix.Length;
			int end = line.IndexOf ('"', start);
			return end < 0 ? "" : line.Substring (start, end - start);
		}

		void AssertRawJavaHolderHasNoManagedBinding (ProjectBuilder builder)
		{
			var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			Assert.IsEmpty (
				Directory.GetFiles (projectDirectory, "*ValueTypeContainerFixture*.cs", SearchOption.AllDirectories),
				"The raw JNI holder must not produce a managed binding that can root closed collection wrappers.");
		}

		static string ReadFixture (string fileName)
		{
			return File.ReadAllText (
				Path.Combine (
					XABuildPaths.TopDirectory,
					"tests",
					"MSBuildDeviceIntegration",
					"Resources",
					"ValueTypeContainerApp",
					fileName));
		}
	}
}
