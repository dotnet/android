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
					apiDescriptionFile:     "expected.ji/NormalProperties/NormalProperties.xml",
					expectedRelativePath:   "NormalProperties");
		}
	}
}

