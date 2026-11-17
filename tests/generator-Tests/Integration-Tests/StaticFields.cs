using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class StaticFields : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "StaticFields",
					apiDescriptionFile:     "TestInputs/StaticFields/StaticField.xml",
					expectedRelativePath:   "StaticFields");
		}
	}
}

