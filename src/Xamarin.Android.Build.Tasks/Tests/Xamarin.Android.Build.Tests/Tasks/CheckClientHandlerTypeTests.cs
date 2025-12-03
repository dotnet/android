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
		static IEnumerable<object[]> Get_ErrorIsNotRaised_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
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
		[TestCaseSource (nameof (Get_ErrorIsNotRaised_Data))]
		public void ErrorIsNotRaised (string handler, AndroidRuntime runtime)
		{
			const bool isRelease = false;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string intermediatePath;
			bool shouldSkip = handler.Contains ("Xamarin.Android.Net.AndroidMessageHandler");
			bool targetSkipped;
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidHttpClientHandlerType", handler);
			using (var b = CreateApkBuilder (path)) {
				b.Verbosity = LoggerVerbosity.Detailed;
				b.Build (proj);
				intermediatePath = Path.Combine (path,proj.IntermediateOutputPath);
				targetSkipped = b.Output.IsTargetSkipped ("_CheckAndroidHttpClientHandlerType", defaultIfNotUsed: shouldSkip);
			}

			if (shouldSkip)
				Assert.IsTrue (targetSkipped, "_CheckAndroidHttpClientHandlerType should not have run.");
			else
				Assert.IsFalse (targetSkipped, "_CheckAndroidHttpClientHandlerType should have run.");

			string asmPath = Path.GetFullPath (Path.Combine (intermediatePath, "android", "assets"));
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
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
		}

		static IEnumerable<object[]> Get_ErrorIsRaised_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
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
