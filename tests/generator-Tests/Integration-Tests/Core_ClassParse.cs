using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Core_ClassParse : BaseGeneratorTest
	{
		[TestMethodAttribute]
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

