using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Build.Tests
{
	public class CheckClientHandlerTypeTests : BaseTest
	{
		static IEnumerable<object[]> Get_DeprecationWarningIsRaised_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in new[] { AndroidRuntime.MonoVM, AndroidRuntime.CoreCLR }) {
				AddTestData ("Xamarin.Android.Net.AndroidMessageHandler", runtime);
				AddTestData ("System.Net.Http.SocketsHttpHandler, System.Net.Http", runtime);
			}

			return ret;

			void AddTestData (string handler, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					handler,
					runtime,
				});
			}
		}

		[Test]
		[TestCaseSource (nameof (Get_DeprecationWarningIsRaised_Data))]
		public void DeprecationWarningIsRaised (string handler, AndroidRuntime runtime)
		{
			const bool isRelease = false;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidHttpClientHandlerType", handler);
			using (var b = CreateApkBuilder (path)) {
				b.Verbosity = LoggerVerbosity.Detailed;
				b.Build (proj);
				// Should emit deprecation warning XA1043 for MonoVM/CoreCLR
				Assert.IsTrue (b.LastBuildOutput.ContainsText ("XA1043"), "Expected deprecation warning XA1043");
			}
		}

		[Test]
		public void NativeAOT_ErrorIsRaised_WhenHttpClientHandlerTypeSet ()
		{
			const bool isRelease = true;
			var runtime = AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidHttpClientHandlerType", "System.Net.Http.SocketsHttpHandler, System.Net.Http");
			using (var b = CreateApkBuilder (path)) {
				b.Verbosity = LoggerVerbosity.Detailed;
				b.ThrowOnBuildFailure = false;
				b.Build (proj);
				// Should emit error XA1042 for NativeAOT
				Assert.IsTrue (b.LastBuildOutput.ContainsText ("XA1042"), "Expected error XA1042 for NativeAOT");
			}
		}

		static IEnumerable<object[]> Get_ErrorIsRaised_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in new[] { AndroidRuntime.MonoVM, AndroidRuntime.CoreCLR }) {
				AddTestData ("Xamarin.Android.Net.AndroidClientHandler", runtime);
			}

			return ret;

			void AddTestData (string handler, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					handler,
					runtime,
				});
			}
		}

		[Test]
		[TestCaseSource (nameof (Get_ErrorIsRaised_Data))]
		public void ErrorIsRaised (string handler, AndroidRuntime runtime)
		{
			const bool isRelease = false;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string intermediatePath;
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.PackageReferences.Add (new Package() { Id = "System.Net.Http", Version = "*" });
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				b.Build (proj);
				intermediatePath = Path.Combine (path, proj.IntermediateOutputPath);
			}
			string asmPath = Path.Combine (intermediatePath, "android", "assets");
			var errors = new List<BuildErrorEventArgs> ();
			var warnings = new List<BuildWarningEventArgs> ();
			List<ITaskItem> assemblies = new List<ITaskItem> ();
			string[] files = Directory.GetFiles (asmPath, "*.dll", SearchOption.AllDirectories);
			foreach (var file in files)
				assemblies.Add (new TaskItem (file));
			IBuildEngine4 engine = new MockBuildEngine (System.Console.Out, errors, warnings);
			var task = new CheckClientHandlerType () {
				BuildEngine = engine,
				ClientHandlerType = handler,
				ResolvedAssemblies = assemblies.ToArray (),
			};
			Assert.False (task.Execute (), $"task should have failed.");
			Assert.AreEqual (1, errors.Count, $"One error should have been raised. {string.Join (" ", errors.Select (e => e.Message))}");
			Assert.AreEqual ("XA1031", errors [0].Code, "Error code should have been XA1031.");
		}
	}
}
