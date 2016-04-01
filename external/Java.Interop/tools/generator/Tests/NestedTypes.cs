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
			RunAllTargets (
					outputRelativePath:     "NestedTypes",
					apiDescriptionFile:     "expected/NestedTypes/NestedTypes.xml",
					expectedRelativePath:   "NestedTypes");
		}
	}
}
