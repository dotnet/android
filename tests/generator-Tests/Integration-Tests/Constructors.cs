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
			RunTarget (
					outputRelativePath:     "Constructors",
					apiDescriptionFile:     "TestInputs/Constructors/Constructors.xml",
					expectedRelativePath:   "Constructors");
		}
	}
}

