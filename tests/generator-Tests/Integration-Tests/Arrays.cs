using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Arrays : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "Arrays",
					apiDescriptionFile:     "TestInputs/Arrays/Arrays.xml",
					expectedRelativePath:   "Arrays");
		}
	}
}

