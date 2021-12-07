using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NestedTypes : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

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
