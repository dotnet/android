using NUnit.Framework;
using System;

namespace generatortests
{
	[TestFixture]
	public class Java_Lang_Enum : BaseGeneratorTest
	{
		[Test]
		public void Generated_OK ()
		{
			//hides inherited member `Java.Lang.Object.class_ref'
			AllowWarnings = true;
			RunAllTargets (
					outputRelativePath:     "java.lang.Enum",
					apiDescriptionFile:     "expected/java.lang.Enum/Java.Lang.Enum.xml",
					expectedRelativePath:   "java.lang.Enum");
		}
	}
}

