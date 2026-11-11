using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class ParameterXPath : BaseGeneratorTest
	{

		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "ParameterXPath",
					apiDescriptionFile:     "TestInputs/ParameterXPath/ParameterXPath.xml",
					expectedRelativePath:   "ParameterXPath");
		}
	}
}

