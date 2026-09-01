using System;
using System.IO;
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
	public abstract void completeTestForIssue12542 ();
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
		string resultValue = "missing";

		public WebViewJavascriptBridge ()
		{
			Identity = "bridge-12542";
		}

		public string Identity { get; }

		public Task<string> Result => result.Task;

		[JavascriptInterface]
		public override void ReportResultForIssue12542 (string value)
		{
			Interlocked.Exchange (ref resultValue, value);
			Interlocked.Increment (ref invocationCount);
		}

		[JavascriptInterface]
		public override void CompleteTestForIssue12542 ()
		{
			// JavaScript calls this after the synchronous report call, so the count includes duplicate dispatches.
			string value = Volatile.Read (ref resultValue);
			int count = Volatile.Read (ref invocationCount);
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
managedBridge.completeTestForIssue12542();
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
			bool installed = false;
			try {
				installed = builder.Install (proj);
				Assert.IsTrue (installed, "Project should have installed.");

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
				StringAssert.Contains (SuccessMarker + SuccessResult, logcat, "The JavaScript report callback should invoke the exact managed bridge instance once.");
				StringAssert.DoesNotContain (FailureMarker, logcat, "WebView reported a JavaScript interface failure.");
			} finally {
				if (installed) {
					try {
						builder.ThrowOnBuildFailure = false;
						if (!builder.Uninstall (proj)) {
							TestContext.Error.WriteLine ($"Failed to uninstall '{proj.PackageName}' during test cleanup.");
						}
					} catch (Exception ex) {
						TestContext.Error.WriteLine ($"Failed to uninstall '{proj.PackageName}' during test cleanup: {ex}");
					}
				}
			}
		}
	}
}
