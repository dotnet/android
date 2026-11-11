using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class StaticMethods : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "StaticMethods",
					apiDescriptionFile:     "TestInputs/StaticMethods/StaticMethod.xml",
					expectedRelativePath:   "StaticMethods");
		}
	}
}

