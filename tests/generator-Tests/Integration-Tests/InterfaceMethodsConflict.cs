using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class InterfaceMethodsConflict : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "InterfaceMethodsConflict",
					apiDescriptionFile:     "TestInputs/InterfaceMethodsConflict/InterfaceMethodsConflict.xml",
					expectedRelativePath:   "InterfaceMethodsConflict");
		}
	}
}

