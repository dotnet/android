using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Core_Jar2Xml : BaseGeneratorTest
	{

		[TestMethodAttribute]
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

