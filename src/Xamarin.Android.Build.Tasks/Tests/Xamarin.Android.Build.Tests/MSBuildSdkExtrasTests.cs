using System.IO;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] // On MacOS, parallel /restore causes issues
	[Category ("Node-3")]
	public class MSBuildSdkExtrasTests : BaseTest
	{
		[Test]
		public void ClassLibrary ()
		{
			var proj = new MSBuildSdkExtrasProject ();
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var output = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.ProjectName + ".dll");
				FileAssert.Exists (output);
				AssertContainsClass (output, "Class1", contains: true);
			}
		}

		[Test]
		public void ClassLibraryNoResources ()
		{
			var proj = new MSBuildSdkExtrasProject ();
			proj.Sources.Remove (proj.Sources.First (s => s.BuildAction == "AndroidResource"));
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var output = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.ProjectName + ".dll");
				FileAssert.Exists (output);
				AssertContainsClass (output, "Class1", contains: true);
			}
		}

		[Test]
		public void BindingProject ()
		{
			var proj = new MSBuildSdkExtrasProject {
				IsBindingProject = true,
			};
			proj.OtherBuildItems.Add (new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
				WebContent = "https://storage.googleapis.com/google-code-archive-downloads/v2/code.google.com/svg-android/svg-android.jar"
			});
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var output = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.ProjectName + ".dll");
				FileAssert.Exists (output);
				AssertContainsClass (output, "Com.Larvalabs.Svgandroid.SVG", contains: true);
			}
		}

		[Test]
		public void MultiTargeting ()
		{
			var proj = new MSBuildSdkExtrasProject ();
			proj.TargetFrameworks += ";netstandard2.0";
			proj.Sources.Add (new BuildItem.Source ("MyView.cs") {
				TextContent = () =>
@"#if __ANDROID__
class MyView : Android.Views.View
{
	public MyView (Android.Content.Context c) : base (c) { }
}
#endif",
			});
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var output = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.ProjectName + ".dll");
				FileAssert.Exists (output);
				AssertContainsClass (output, "MyView", contains: true);
				output = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, "..", "netstandard2.0", proj.ProjectName + ".dll");
				FileAssert.Exists (output);
				AssertContainsClass (output, "MyView", contains: false);
			}
		}

		void AssertContainsClass (string assemblyFile, string className, bool contains)
		{
			using (var assembly = AssemblyDefinition.ReadAssembly(assemblyFile)) {
				bool result = assembly.MainModule.Types.Select (t => t.FullName).Contains (className);
				if (contains) {
					Assert.IsTrue (result, $"{assemblyFile} should contain {className}!");
				} else {
					Assert.IsFalse (result, $"{assemblyFile} should *not* contain {className}!");
				}
			}
		}
	}
}
