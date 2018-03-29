using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class StaticProperties : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "StaticProperties",
					apiDescriptionFile:     "expected/StaticProperties/StaticProperties.xml",
					expectedRelativePath:   "StaticProperties");
		}
	}
}

