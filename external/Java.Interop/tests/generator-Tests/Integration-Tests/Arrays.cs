using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Arrays : BaseGeneratorTest
	{
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

