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
		[Test]
		[TestCase ("Xamarin.Android.Net.AndroidMessageHandler")]
		[TestCase ("System.Net.Http.SocketsHttpHandler, System.Net.Http")]
		public void ErrorIsNotRaised (string handler)
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string intermediatePath;
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
			};
			proj.PackageReferences.Add (new Package() { Id = "System.Net.Http", Version = "*" });
			using (var b = CreateApkBuilder (path)) {
				b.ThrowOnBuildFailure = false;
				b.Build (proj); // we don't care it might error.
				intermediatePath = Path.Combine (path,proj.IntermediateOutputPath);
			}
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

		[Test]
		[TestCase ("Xamarin.Android.Net.AndroidClientHandler")]
		public void ErrorIsRaised (string handler)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string intermediatePath;
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
			};
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
