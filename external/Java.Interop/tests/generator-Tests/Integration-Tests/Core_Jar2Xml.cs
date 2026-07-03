using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Core_Jar2Xml : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

		[Test]
		public void GeneratedOK ()
		{
			AllowWarnings = true;

			RunAllTargets (
					outputRelativePath: "Core_Jar2Xml",
					apiDescriptionFile: "expected.ji/Core_Jar2Xml/api.xml",
					expectedRelativePath: "Core_Jar2Xml",
					enumFieldsMapFile: "expected.ji/Core_Jar2Xml/fields.xml",
					enumMethodMapFile: "expected.ji/Core_Jar2Xml/methods.xml"
					);
		}
	}
}

