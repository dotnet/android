using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class StaticProperties : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "StaticProperties",
					apiDescriptionFile:     "TestInputs/StaticProperties/StaticProperties.xml",
					expectedRelativePath:   "StaticProperties");
		}
	}
}

