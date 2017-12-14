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
			//hides inherited member `Java.Lang.Object.class_ref'
			AllowWarnings = true;
			RunAllTargets (
					outputRelativePath:     "TestInterface",
					apiDescriptionFile:     "expected/TestInterface/TestInterface.xml",
					expectedRelativePath:   "TestInterface");
		}
	}
}

