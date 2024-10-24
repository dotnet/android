using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Java_Util_List : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => true;

		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "java.util.List",
					apiDescriptionFile:     "expected.ji/java.util.List/java.util.List.xml",
					expectedRelativePath:   "java.util.List");
		}
	}
}

