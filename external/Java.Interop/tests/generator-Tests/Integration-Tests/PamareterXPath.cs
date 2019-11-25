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
			RunAllTargets (
					outputRelativePath:     "ParameterXPath",
					apiDescriptionFile:     "expected/ParameterXPath/ParameterXPath.xml",
					expectedRelativePath:   "ParameterXPath");
		}
	}
}

