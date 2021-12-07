using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Arrays : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "Arrays",
					apiDescriptionFile:     "expected/Arrays/Arrays.xml",
					expectedRelativePath:   "Arrays");
		}
	}
}

