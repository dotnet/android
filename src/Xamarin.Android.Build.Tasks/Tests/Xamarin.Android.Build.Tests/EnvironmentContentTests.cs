using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class EnvironmentContent : BaseTest
	{
		[Test]
		public void CheckConcurrentGC ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			var gcVarName = "MONO_GC_PARAMS";
			var expectedDefaultValue = "major=marksweep";
			var expectedUpdatedValue = "major=marksweep-conc";
			var supportedAbis = "armeabi-v7a;arm64-v8a";
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, supportedAbis);

			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				// AndroidEnableSGenConcurrent=False by default
				List<string> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (gcVarName), $"Environment should contain '{gcVarName}'.");
				Assert.AreEqual (expectedDefaultValue, envvars[gcVarName], $"'{gcVarName}' should have been '{expectedDefaultValue}' when concurrent GC is disabled.");

				proj.SetProperty ("AndroidEnableSGenConcurrent", "True");
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (gcVarName), $"Environment should contain '{gcVarName}'.");
				Assert.AreEqual (expectedUpdatedValue, envvars[gcVarName], $"'{gcVarName}' should have been '{expectedUpdatedValue}' when concurrent GC is enabled.");
			}
		}

		[Test]
		public void CheckHttpClientHandlerType ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			var httpClientHandlerVarName = "XA_HTTP_CLIENT_HANDLER_TYPE";
			var expectedDefaultValue = "System.Net.Http.HttpClientHandler, System.Net.Http";
			var expectedUpdatedValue = "Xamarin.Android.Net.AndroidClientHandler";
			var supportedAbis = "armeabi-v7a;arm64-v8a";
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, supportedAbis);

			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				List<string> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (httpClientHandlerVarName), $"Environment should contain '{httpClientHandlerVarName}'.");
				Assert.AreEqual (expectedDefaultValue, envvars[httpClientHandlerVarName]);

				proj.SetProperty ("AndroidHttpClientHandlerType", expectedUpdatedValue);
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (httpClientHandlerVarName), $"Environment should contain '{httpClientHandlerVarName}'.");
				Assert.AreEqual (expectedUpdatedValue, envvars[httpClientHandlerVarName]);
			}
		}

	}
}
