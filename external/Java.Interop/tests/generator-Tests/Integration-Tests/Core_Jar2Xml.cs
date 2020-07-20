using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Core_Jar2Xml : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			AllowWarnings = true;

			RunAllTargets (
					outputRelativePath: "Core_Jar2Xml",
					apiDescriptionFile: "expected/Core_Jar2Xml/api.xml",
					expectedRelativePath: "Core_Jar2Xml",
					enumFieldsMapFile: "expected/Core_Jar2Xml/fields.xml",
					enumMethodMapFile: "expected/Core_Jar2Xml/methods.xml"
					);
		}
	}
}

