using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NormalProperties : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "NormalProperties",
					apiDescriptionFile:     "expected/NormalProperties/NormalProperties.xml",
					expectedRelativePath:   "NormalProperties");
		}
	}
}

