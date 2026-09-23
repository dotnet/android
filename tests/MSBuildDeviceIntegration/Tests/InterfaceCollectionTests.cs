using System;
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
	public class InterfaceCollectionTests : DeviceTest
	{
		const string ResultPrefix = "INTERFACE_COLLECTION_ROOTING_RESULT";

		[Test]
		public void InterfaceCollectionFactoryRootsCanonicalWrappers ()
		{
			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (AndroidRuntime.NativeAOT, "interfacecollectionrooting")) {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers ([DeviceAbi]);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("AndroidSdkDirectory", AndroidSdkResolver.GetAndroidSdkPath ());
			var javaSdkDirectory = AndroidSdkResolver.GetJavaSdkPath ();
			proj.SetProperty ("JavaSdkDirectory", javaSdkDirectory);
			proj.SetProperty ("JavaCPath", Path.Combine (javaSdkDirectory, "bin", "javac"));
			proj.SetProperty ("JarPath", Path.Combine (javaSdkDirectory, "bin", "jar"));
			proj.SetDefaultTargetDevice ();
			var resultToken = Guid.NewGuid ().ToString ("N");
			proj.MainActivity = proj.ProcessSourceTemplate (
				ReadFixture ("MainActivity.cs").Replace ("${RESULT_TOKEN}", resultToken, StringComparison.Ordinal));
			proj.Sources.Add (new BuildItem.Source ("RawInterfaceCollectionHolder.cs") {
				TextContent = () => ReadRuntimeFixture (Path.Combine ("Java.Interop", "RawInterfaceCollectionHolder.cs")),
			});
			proj.AndroidJavaSources.Add (CreateJavaSource ("ValueProvider.java", bind: true));
			proj.AndroidJavaSources.Add (CreateJavaSource ("ExtendedValueProvider.java", bind: true));
			proj.AndroidJavaSources.Add (CreateJavaSource ("InterfaceCollectionFixture.java", bind: false));
			proj.OtherBuildItems.Add (new AndroidItem.ProguardConfiguration ("proguard.cfg") {
				TextContent = () => ReadRuntimeFixture ("InterfaceCollection.proguard.cfg"),
			});

			var testDirectory = Path.Combine ("temp", nameof (InterfaceCollectionFactoryRootsCanonicalWrappers));
			using var builder = CreateApkBuilder (testDirectory);
			try {
				Assert.IsTrue (builder.Install (proj), "The focused interface-collection app should install.");
				AssertGeneratedBindingsAreIsolated (builder, proj);

				ClearAdbLogcat ();
				var logcatPath = Path.Combine (Root, builder.ProjectDirectory, "interface-collections-logcat.log");
				string resultLine = "";
				bool resultFound = MonitorAdbLogcat (line => {
					if (!line.Contains (ResultPrefix, StringComparison.Ordinal) ||
							!line.Contains (resultToken, StringComparison.Ordinal)) {
						return false;
					}
					resultLine = line;
					return true;
				}, logcatPath, ActivityStartTimeoutInSeconds, onMonitoringStarted: () => StartActivityAndAssert (proj));
				Assert.IsTrue (resultFound, $"The focused app did not report a result. See '{logcatPath}'.");
				StringAssert.Contains ($"{ResultPrefix} PASS {resultToken}", resultLine);
			} finally {
				RunAdbCommand ($"uninstall {proj.PackageName}");
			}
		}

		static AndroidItem.AndroidJavaSource CreateJavaSource (string fileName, bool bind)
		{
			var path = Path.Combine ("java", "net", "dot", "android", "test", fileName);
			return new AndroidItem.AndroidJavaSource (path) {
				Encoding = Encoding.ASCII,
				TextContent = () => ReadRuntimeFixture (path),
				Metadata = {
					{ "Bind", bind.ToString () },
				},
			};
		}

		void AssertGeneratedBindingsAreIsolated (ProjectBuilder builder, XamarinAndroidApplicationProject proj)
		{
			var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			var generatedSourceDirectory = Path.Combine (projectDirectory, proj.IntermediateOutputPath, "generated", "src");
			FileAssert.Exists (Path.Combine (generatedSourceDirectory, "Net.Dot.Android.Test.IValueProvider.cs"));
			FileAssert.Exists (Path.Combine (generatedSourceDirectory, "Net.Dot.Android.Test.IExtendedValueProvider.cs"));
			Assert.IsEmpty (
				Directory.GetFiles (generatedSourceDirectory, "*InterfaceCollection*.cs", SearchOption.TopDirectoryOnly),
				"The raw JNI holder and concrete peers must not produce managed bindings that can root closed collection wrappers.");
		}

		static string ReadFixture (string fileName)
		{
			return File.ReadAllText (
				Path.Combine (
					XABuildPaths.TopDirectory,
					"tests",
					"MSBuildDeviceIntegration",
					"Resources",
					"InterfaceCollectionApp",
					fileName));
		}

		static string ReadRuntimeFixture (string fileName)
		{
			return File.ReadAllText (
				Path.Combine (XABuildPaths.TopDirectory, "tests", "Mono.Android-Tests", "Mono.Android-Tests", fileName));
		}
	}
}
