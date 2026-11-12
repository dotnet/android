using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Java_Util_List : BaseGeneratorTest
	{

		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "java.util.List",
					apiDescriptionFile:     "TestInputs/java.util.List/java.util.List.xml",
					expectedRelativePath:   "java.util.List");
		}
	}
}

