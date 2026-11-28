using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Adapters : BaseGeneratorTest
	{

		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "Adapters",
					apiDescriptionFile:     "TestInputs/Adapters/Adapters.xml",
					expectedRelativePath:   "Adapters",
					additionalSupportPaths: new[]{ "TestInputs/Adapters/SupportFiles" });
		}
	}
}

