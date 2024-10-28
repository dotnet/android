using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NestedTypes : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => true;

		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "NestedTypes",
					apiDescriptionFile:     "expected.ji/NestedTypes/NestedTypes.xml",
					expectedRelativePath:   "NestedTypes");
		}
	}
}
