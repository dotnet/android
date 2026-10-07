using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class ParameterXPath : BaseGeneratorTest
	{

		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "ParameterXPath",
					apiDescriptionFile:     "TestInputs/ParameterXPath/ParameterXPath.xml",
					expectedRelativePath:   "ParameterXPath");
		}
	}
}

