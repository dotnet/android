using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestFixture]
	public class MamJsonToXmlTests : BaseTest {

		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out, errors = new List<BuildErrorEventArgs> (), warnings = new List<BuildWarningEventArgs> ());
		}

		[Test]
		public void TestMamJsonIsParsedToXml ()
		{
			var log = new TaskLoggingHelper (engine, TestName);
			File.WriteAllText ("input.json", ResourceData.RemapMamJson);
			var task = new MamJsonToXml {
				BuildEngine = engine,
				MappingFiles = new ITaskItem [] {
					new TaskItem ( "input.json"),
				},
				XmlMappingOutput = new TaskItem ("output.xml"),
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "Task should have no errors.");
			Assert.AreEqual (0, warnings.Count);
			FileAssert.Exists (task.XmlMappingOutput.ItemSpec, $"The expected XML file '{task.XmlMappingOutput.ItemSpec}' does not exist.");
			var xml = File.ReadAllText (task.XmlMappingOutput.ItemSpec);
			StringAssertEx.AreMultiLineEqual (ResourceData.RemapMamXml, xml);
		}
	}
}