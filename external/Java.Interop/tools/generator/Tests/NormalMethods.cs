using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NormalMethods : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "NormalMethods",
					apiDescriptionFile:     "expected/NormalMethods/NormalMethods.xml",
					expectedRelativePath:   "NormalMethods");
		}
	}
}

