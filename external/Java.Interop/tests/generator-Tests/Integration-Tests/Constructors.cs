using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Constructors : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "Constructors",
					apiDescriptionFile:     "expected/Constructors/Constructors.xml",
					expectedRelativePath:   "Constructors");
		}
	}
}

