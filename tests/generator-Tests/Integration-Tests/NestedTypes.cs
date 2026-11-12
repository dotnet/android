using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class NestedTypes : BaseGeneratorTest
	{

		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "NestedTypes",
					apiDescriptionFile:     "TestInputs/NestedTypes/NestedTypes.xml",
					expectedRelativePath:   "NestedTypes");
		}
	}
}
