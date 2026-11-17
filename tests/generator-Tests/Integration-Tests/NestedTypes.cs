using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NestedTypes : BaseGeneratorTest
	{

		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "NestedTypes",
					apiDescriptionFile:     "TestInputs/NestedTypes/NestedTypes.xml",
					expectedRelativePath:   "NestedTypes");
		}
	}
}
