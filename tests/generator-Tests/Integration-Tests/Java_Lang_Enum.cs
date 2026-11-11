using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace generatortests
{
	[TestClassAttribute]
	public class Java_Lang_Enum : BaseGeneratorTest
	{

		[TestMethodAttribute]
		public void Generated_OK ()
		{
			RunTarget (
					outputRelativePath:     "java.lang.Enum",
					apiDescriptionFile:     "TestInputs/java.lang.Enum/Java.Lang.Enum.xml",
					expectedRelativePath:   "java.lang.Enum");
		}
	}
}

