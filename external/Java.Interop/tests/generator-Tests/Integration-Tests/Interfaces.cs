using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Interfaces : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

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

