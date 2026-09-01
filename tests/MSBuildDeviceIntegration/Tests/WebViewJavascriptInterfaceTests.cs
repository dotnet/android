using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class WebViewJavascriptInterfaceTests : DeviceTest
	{
		const string JavaMethodName = "reportResultForIssue12542";
		const string JavascriptInterfaceAnnotation = "Landroid/webkit/JavascriptInterface;";
		const string SuccessResult = "bridge-12542:payload-12542:1";
		const string SuccessMarker = "# JAVASCRIPT_INTERFACE_RESULT ";
		const string FailureMarker = "# JAVASCRIPT_INTERFACE_FAILURE ";

		[Test]
		public void LocalJavascriptInvokesManagedBridge_LlvmIrCoreClr ()
		{
			LocalJavascriptInvokesManagedBridge ("llvm-ir", AndroidRuntime.CoreCLR);
		}

		[Test]
		public void LocalJavascriptInvokesManagedBridge_TrimmableCoreClr ()
		{
			LocalJavascriptInvokesManagedBridge ("trimmable", AndroidRuntime.CoreCLR);
		}

		[Test]
		public void LocalJavascriptInvokesManagedBridge_TrimmableNativeAot ()
		{
			LocalJavascriptInvokesManagedBridge ("trimmable", AndroidRuntime.NativeAOT);
		}

		void LocalJavascriptInvokesManagedBridge (string typemapImplementation, AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var packageSuffix = $"javascriptinterface{typemapImplementation.Replace ("-", "")}";
			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime, packageSuffix)) {
				IsRelease = isRelease,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new AndroidItem.AndroidJavaSource ("WebViewJavascriptBridgeBase.java") {
						Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
						TextContent = () => """
package example;

public abstract class WebViewJavascriptBridgeBase {
	public WebViewJavascriptBridgeBase () {
	}

	public abstract void reportResultForIssue12542 (String value);
}
""",
						Metadata = {
							{ "Bind", "True" },
						},
					},
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
			proj.SetProperty ("AndroidTypeMapImplementation", typemapImplementation);
			proj.SetDefaultTargetDevice ();
			proj.Sources.Add (new BuildItem.Source ("WebViewJavascriptBridge.cs") {
				TextContent = () => """
using System.Threading;
using System.Threading.Tasks;
using Android.Webkit;

namespace UnnamedProject
{
	public sealed class WebViewJavascriptBridge : Example.WebViewJavascriptBridgeBase
	{
		readonly TaskCompletionSource<string> result = new TaskCompletionSource<string> (TaskCreationOptions.RunContinuationsAsynchronously);
		int invocationCount;

		public WebViewJavascriptBridge ()
		{
			Identity = "bridge-12542";
		}

		public string Identity { get; }

		public Task<string> Result => result.Task;

		[JavascriptInterface]
		public override void ReportResultForIssue12542 (string value)
		{
			int count = Interlocked.Increment (ref invocationCount);
			result.TrySetResult ($"{Identity}:{value}:{count}");
		}
	}
}
""",
			});
			proj.MainActivity = """"
using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Webkit;

namespace UnnamedProject
{
	[Activity (Label = "UnnamedProject", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			RunJavascriptInterfaceTest ();
		}

		async void RunJavascriptInterfaceTest ()
		{
			const string interfaceName = "managedBridge";
			const string expectedResult = "bridge-12542:payload-12542:1";
			const string successMarker = "# JAVASCRIPT_INTERFACE_RESULT ";
			const string failureMarker = "# JAVASCRIPT_INTERFACE_FAILURE ";
			const string html = """
<!doctype html>
<html>
<body>
<script>
managedBridge.reportResultForIssue12542("payload-12542");
</script>
</body>
</html>
""";
			var bridge = new WebViewJavascriptBridge ();
			var webView = new WebView (this);
			string completion = failureMarker + "unknown";
			try {
				if (Looper.MyLooper () != Looper.MainLooper) {
					throw new InvalidOperationException ("WebView was not created on the UI thread.");
				}
				webView.Settings.JavaScriptEnabled = true;
				webView.AddJavascriptInterface (bridge, interfaceName);
				SetContentView (webView);
				webView.LoadDataWithBaseURL ("https://localhost/", html, "text/html", "UTF-8", null);

				using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (15));
				string result = await bridge.Result.WaitAsync (timeout.Token);
				completion = result == expectedResult
					? successMarker + result
					: failureMarker + "unexpected-result:" + result;
			} catch (System.OperationCanceledException) {
				completion = failureMarker + "timeout";
			} catch (Exception ex) {
				completion = failureMarker + ex.GetType ().FullName + ":" + ex.Message;
			} finally {
				RunOnUiThread (() => {
					webView.RemoveJavascriptInterface (interfaceName);
					webView.StopLoading ();
					webView.Destroy ();
					webView.Dispose ();
					bridge.Dispose ();
					Console.WriteLine (completion);
				});
			}
		}
	}
}
"""";

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Project should have installed.");

			var projectIntermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var generatedJavaFiles = Directory.GetFiles (projectIntermediate, "WebViewJavascriptBridge.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (generatedJavaFiles, $"Expected a generated WebViewJavascriptBridge.java under '{projectIntermediate}'.");
			foreach (var generatedJavaFile in generatedJavaFiles) {
				var contents = File.ReadAllText (generatedJavaFile).Replace ("\r\n", "\n");
				StringAssert.Contains ("@android.webkit.JavascriptInterface", contents,
					$"Generated JCW Java '{generatedJavaFile}' should forward [JavascriptInterface].");
				StringAssert.Contains ($"public void {JavaMethodName} (java.lang.String p0)", contents,
					$"Generated JCW Java '{generatedJavaFile}' should expose the bridge method.");
			}

			var generatedJava = File.ReadAllText (generatedJavaFiles [0]).Replace ("\r\n", "\n");
			var packageDeclaration = generatedJava.Split ('\n').Single (line => line.StartsWith ("package ", StringComparison.Ordinal));
			var javaPackage = packageDeclaration.Substring ("package ".Length).TrimEnd (';');
			var className = $"L{javaPackage.Replace ('.', '/')}/WebViewJavascriptBridge;";
			var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
			FileAssert.Exists (dexFile);
			Assert.IsTrue (DexUtils.ContainsClassWithMethod (className, JavaMethodName, "(Ljava/lang/String;)V", dexFile, AndroidSdkPath),
				$"`{dexFile}` should contain `{className}.{JavaMethodName}`.");
			Assert.IsTrue (DexUtils.ContainsRuntimeMethodAnnotation (JavaMethodName, JavascriptInterfaceAnnotation, dexFile, AndroidSdkPath),
				$"`{dexFile}` should retain `{JavascriptInterfaceAnnotation}` on `{JavaMethodName}`.");

			ClearAdbLogcat ();
			RunProjectAndAssert (proj, builder, doNotCleanupOnUpdate: true);
			Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "activity-logcat.log"), ActivityStartTimeoutInSeconds), "Activity should have started.");

			var resultLog = Path.Combine (Root, builder.ProjectDirectory, "javascript-interface-logcat.log");
			Assert.IsTrue (MonitorAdbLogcat (
				line => line.Contains (SuccessMarker, StringComparison.Ordinal) || line.Contains (FailureMarker, StringComparison.Ordinal),
				resultLog,
				timeout: 30), "WebView did not report a JavaScript interface result.");
			var logcat = File.ReadAllText (resultLog);
			StringAssert.Contains (SuccessMarker + SuccessResult, logcat, "Local JavaScript should invoke the exact managed bridge instance once.");
			StringAssert.DoesNotContain (FailureMarker, logcat, "WebView reported a JavaScript interface failure.");
			Assert.IsTrue (builder.Uninstall (proj), "Project should have uninstalled.");
		}
	}
}
