using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Core_ClassParse : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			AllowWarnings = true;

			RunTarget (
					outputRelativePath: "Core_ClassParse",
					apiDescriptionFile: "TestInputs/Core_ClassParse/api.xml",
					expectedRelativePath: "Core_ClassParse",
					metadataFile: FullPath ("TestInputs/Core_ClassParse/metadata.xml"));
		}
	}
}

