using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestFixture]
	public class MamJsonToXmlTests : BaseTest {

		static string ResourcePath (string name) =>
			Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "MamJsonToXml", name);

		[Test]
		public void TaskRegistrationsUseAttributeFreeCoreAndNonCoreSplit ()
		{
			var source = XDocument.Load (Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
			var registrations = source.Descendants ()
				.Where (element => element.Name.LocalName == "UsingTask" &&
					((string?) element.Attribute ("TaskName"))?.EndsWith (".MamJsonToXml") == true)
				.ToArray ();
			Assert.AreEqual (2, registrations.Length);
			var modern = registrations.Single (element => (string?) element.Attribute ("TaskName") == "Microsoft.Android.Tasks.MamJsonToXml");
			var legacy = registrations.Single (element => (string?) element.Attribute ("TaskName") == "Xamarin.Android.Tasks.MamJsonToXml");
			Assert.AreEqual ("'$(MSBuildRuntimeType)' == 'Core'", ((string?) modern.Attribute ("Condition"))?.Trim ());
			Assert.AreEqual ("'$(MSBuildRuntimeType)' != 'Core'", ((string?) legacy.Attribute ("Condition"))?.Trim ());
			Assert.AreEqual ("$(_MicrosoftAndroidBuildTasksAssembly)", (string?) modern.Attribute ("AssemblyFile"));
			Assert.AreEqual ("Xamarin.Android.Build.Tasks.dll", (string?) legacy.Attribute ("AssemblyFile"));
			foreach (var registration in registrations) {
				Assert.IsNull (registration.Attribute ("TaskFactory"));
				Assert.IsNull (registration.Attribute ("Runtime"));
			}
		}

		[Test]
		public void TestMamJsonIsParsedToXml ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var warnings = new List<BuildWarningEventArgs> ();
			var engine = new MockBuildEngine (TestContext.Out, errors, warnings);
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			File.Copy (ResourcePath ("mam.json"), Path.Combine (path, "input.json"), overwrite: true);
			var task = new MamJsonToXml {
				BuildEngine = engine,
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (path, new Dictionary<string, string> ()),
				MappingFiles = new ITaskItem [] {
					new TaskItem ("input.json"),
				},
				XmlMappingOutput = new TaskItem ("output.xml"),
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "Task should have no errors.");
			Assert.AreEqual (0, warnings.Count);
			var output = Path.Combine (path, "output.xml");
			FileAssert.Exists (output, $"The expected XML file '{output}' does not exist.");
			var expected = File.ReadAllText (ResourcePath ("mam.xml")).ReplaceLineEndings ();
			var xml = File.ReadAllText (output).ReplaceLineEndings ();
			Assert.AreEqual (expected, xml);
		}
	}
}
