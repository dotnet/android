using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Java_Util_List : BaseGeneratorTest
	{

		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "java.util.List",
					apiDescriptionFile:     "TestInputs/java.util.List/java.util.List.xml",
					expectedRelativePath:   "java.util.List");
		}
	}
}

