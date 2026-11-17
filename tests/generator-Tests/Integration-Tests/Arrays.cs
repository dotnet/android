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
			RunTarget (
					outputRelativePath:     "Arrays",
					apiDescriptionFile:     "TestInputs/Arrays/Arrays.xml",
					expectedRelativePath:   "Arrays");
		}
	}
}

