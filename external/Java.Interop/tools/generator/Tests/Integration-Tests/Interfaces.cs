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
			RunAllTargets (
					outputRelativePath:     "TestInterface",
					apiDescriptionFile:     "expected/TestInterface/TestInterface.xml",
					expectedRelativePath:   "TestInterface");
		}
	}
}

