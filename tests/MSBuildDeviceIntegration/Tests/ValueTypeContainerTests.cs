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
			} finally {
				RunAdbCommand ($"uninstall {proj.PackageName}");
			}
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
