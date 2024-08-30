using System.IO;
using System.Linq;

using NUnit.Framework;

using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class AndroidGradleProjectReferenceTests : BaseTest
	{
		[Test]
		public void BuildAndBind ()
		{
			var gradlePackageName = "com.example.gradletest";
			var gradleBindingJavaSrc = new AndroidItem.AndroidJavaSource ("TestClass.java") {
				TextContent = () => $@"
package {gradlePackageName};
public class TestClass {{
	public static String getString(String myString) {{
		return myString + "" from java!"";
	}}
}}"
			};
			// TODO Clean up side by side gradle projects
			var gradleProjectDir = Path.Combine (Root, "temp", "GradleTestBuildAndBind");
			var gradleBindingModule = new AndroidGradleLibraryModule (Path.Combine (gradleProjectDir, "BindingTest")){
				JavaSources = { gradleBindingJavaSrc },
			};
			var gradleProject = new AndroidGradleProject (gradleProjectDir) {
				Modules = { gradleBindingModule },
			};
			gradleProject.Create ();

			var gradleReference = new BuildItem ("AndroidGradleProjectReference", gradleProjectDir);
			gradleReference.Metadata.Add ("Module", gradleProject.Modules.First ().Name);
			var proj = new XamarinAndroidBindingProject {
				Jars = { gradleReference },
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => @"public class Foo { public Foo () { System.Console.WriteLine (GradleTest.TestClass.GetString(""TestString"")); } }"
					},
				},
				MetadataXml = $@"<metadata><attr path=""/api/package[@name='{gradlePackageName}']"" name=""managedName"">GradleTest</attr></metadata>",
			};

			using var builder = CreateDllBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, "BindingTest-Release.aar"));
		}

	}
}
