using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Interfaces : BaseGeneratorTest
	{
		[Test]
		public void Generated_OK ()
		{
			AllowWarnings = true;
			RunAllTargets (
					outputRelativePath:     "TestInterface",
					apiDescriptionFile:     "expected/TestInterface/TestInterface.xml",
					expectedRelativePath:   "TestInterface");
		}
	}
}

