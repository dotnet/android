using System.Collections.Generic;
using System.IO;
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
