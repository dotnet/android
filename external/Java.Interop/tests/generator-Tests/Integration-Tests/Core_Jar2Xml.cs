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

			RunTarget (
					outputRelativePath: "Core_Jar2Xml",
					apiDescriptionFile: "TestInputs/Core_Jar2Xml/api.xml",
					expectedRelativePath: "Core_Jar2Xml",
					enumFieldsMapFile: "TestInputs/Core_Jar2Xml/fields.xml",
					enumMethodMapFile: "TestInputs/Core_Jar2Xml/methods.xml"
					);
		}
	}
}

