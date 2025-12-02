using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public partial class SingleProjectTest : BaseTest
	{
		static IEnumerable<object[]> Get_AndroidManifestProperties_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
				AddTestData (
					// TODO: this has changed across all the runtimes from the previous "2.1" to its current value.
					//       Check if it's a valid change.
					versionName: "2.1+29f8376059d032c8eb436e757b146148a71d069e",
					versionCode: "42",
					errorMessage: "",
					runtime: runtime
				);

				AddTestData (
					versionName: "1.0.0",
					versionCode: "1.0.0",
					errorMessage: "XA0003",
					runtime: runtime
				);

				AddTestData (
					versionName: "3.1.3a1",
					versionCode: "42",
					errorMessage: "",
					runtime: runtime
				);

				AddTestData (
					versionName: "6.0-preview.7",
					versionCode: "42",
					errorMessage: "",
					runtime: runtime
				);
			}

			return ret;

			void AddTestData (string versionName, string versionCode, string errorMessage, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					versionName,
					versionCode,
					errorMessage,
					runtime,
				});
			}
		}

		[Test]
		[TestCaseSource (nameof (Get_AndroidManifestProperties_Data))]
		public void AndroidManifestProperties (string versionName, string versionCode, string errorMessage, AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var packageName = "com.xamarin.singleproject";
			var applicationLabel = "My Sweet App";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);

			proj.AndroidManifest = proj.AndroidManifest
				.Replace ("package=\"${PACKAGENAME}\"", "")
				.Replace ("android:label=\"${PROJECT_NAME}\"", "")
				.Replace ("android:versionName=\"1.0\"", "")
				.Replace ("android:versionCode=\"1\"", "");

			proj.SetProperty ("ApplicationId", packageName);
			proj.SetProperty ("ApplicationTitle", applicationLabel);
			proj.SetProperty ("ApplicationVersion", versionCode);
			proj.SetProperty ("ApplicationDisplayVersion", versionName);

			using (var b = CreateApkBuilder ()) {
				if (!string.IsNullOrEmpty (errorMessage)) {
					b.ThrowOnBuildFailure = false;
					Assert.IsFalse (b.Build (proj), "Build should have failed.");
					StringAssertEx.Contains (errorMessage, b.LastBuildOutput, $"Build should fail with message '{errorMessage}'");
					return;
				}

				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var manifest = b.Output.GetIntermediaryPath ("android/AndroidManifest.xml");
				FileAssert.Exists (manifest);

				using (var stream = File.OpenRead (manifest)) {
					var doc = XDocument.Load (stream);
					Assert.AreEqual (packageName, doc.Root.Attribute ("package")?.Value);
					Assert.AreEqual (versionName, doc.Root.Attribute (AndroidAppManifest.AndroidXNamespace + "versionName")?.Value);
					Assert.AreEqual (versionCode, doc.Root.Attribute (AndroidAppManifest.AndroidXNamespace + "versionCode")?.Value);
					Assert.AreEqual (applicationLabel, doc.Root.Element("application").Attribute (AndroidAppManifest.AndroidXNamespace + "label")?.Value);
				}

				string packageExtension = runtime switch {
					AndroidRuntime.NativeAOT => "aab",
					_ => "apk"
				};
				var apk = b.Output.GetIntermediaryPath ($"android/bin/{packageName}.{packageExtension}");
				FileAssert.Exists (apk);

				// If not valid version, skip
				if (!Version.TryParse (versionName, out _))
					return;

				int index = versionName.IndexOf ('-');
				var versionNumber = index == -1 ?
					$"{versionName}.0.0" :
					$"{versionName.Substring (0, index)}.0.0";

				foreach (string abi in proj.GetRuntimeIdentifiersAsAbis ()) {
					string assemblyPath = runtime switch {
						AndroidRuntime.NativeAOT => b.Output.GetIntermediaryPath ($"{MonoAndroidHelper.AbiToRid (abi)}/linked/{proj.ProjectName}.dll"),
						_ => b.Output.GetIntermediaryPath ($"android/assets/{abi}/{proj.ProjectName}.dll")
					};
					FileAssert.Exists (assemblyPath);
					using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath);

					// System.Reflection.AssemblyVersion
					Assert.AreEqual (versionNumber, assembly.Name.Version.ToString ());

					// System.Reflection.AssemblyFileVersion
					var assemblyInfoVersion = assembly.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "System.Reflection.AssemblyInformationalVersionAttribute");
					Assert.IsNotNull (assemblyInfoVersion, "Should find AssemblyInformationalVersionAttribute!");
					Assert.AreEqual (versionName, assemblyInfoVersion.ConstructorArguments [0].Value);

					// System.Reflection.AssemblyInformationalVersion
					var assemblyFileVersion = assembly.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "System.Reflection.AssemblyFileVersionAttribute");
					Assert.IsNotNull (assemblyFileVersion, "Should find AssemblyFileVersionAttribute!");
					Assert.AreEqual (versionNumber, assemblyFileVersion.ConstructorArguments [0].Value);
				}
			}
		}

		[Test]
		public void AndroidManifestValuesWin ()
		{
			var packageName = "com.xamarin.singleproject";
			var applicationLabel = "My Sweet App";
			var versionName = "99.0";
			var versionCode = "99";
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidManifest = proj.AndroidManifest
				.Replace ("package=\"${PACKAGENAME}\"", $"package=\"{packageName}\"")
				.Replace ("android:label=\"${PROJECT_NAME}\"", $"android:label=\"{applicationLabel}\"")
				.Replace ("android:versionName=\"1.0\"", $"android:versionName=\"{versionName}\"")
				.Replace ("android:versionCode=\"1\"", $"android:versionCode=\"{versionCode}\"");

			proj.SetProperty ("ApplicationId", "com.i.should.not.be.used");
			proj.SetProperty ("ApplicationTitle", "I should not be used");
			proj.SetProperty ("ApplicationVersion", "21");
			proj.SetProperty ("ApplicationDisplayVersion", "1.1.1.1");

			using var b = CreateApkBuilder ();
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			var manifest = b.Output.GetIntermediaryPath ("android/AndroidManifest.xml");
			FileAssert.Exists (manifest);

			using var stream = File.OpenRead (manifest);
			var doc = XDocument.Load (stream);
			Assert.AreEqual (packageName, doc.Root.Attribute ("package")?.Value);
			Assert.AreEqual (versionName, doc.Root.Attribute (AndroidAppManifest.AndroidXNamespace + "versionName")?.Value);
			Assert.AreEqual (versionCode, doc.Root.Attribute (AndroidAppManifest.AndroidXNamespace + "versionCode")?.Value);
			Assert.AreEqual (applicationLabel, doc.Root.Element ("application").Attribute (AndroidAppManifest.AndroidXNamespace + "label")?.Value);
		}
	}
}
