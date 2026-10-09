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
			RunTarget (
					outputRelativePath:     "java.lang.Enum",
					apiDescriptionFile:     "TestInputs/java.lang.Enum/Java.Lang.Enum.xml",
					expectedRelativePath:   "java.lang.Enum");
		}
	}
}

