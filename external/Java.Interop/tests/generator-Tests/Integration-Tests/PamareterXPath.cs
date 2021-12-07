using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class ParameterXPath : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

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

