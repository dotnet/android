using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class NonStaticFields : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "NonStaticFields",
					apiDescriptionFile:     "TestInputs/NonStaticFields/NonStaticField.xml",
					expectedRelativePath:   "NonStaticFields");
		}
	}
}

