using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class AccessModifiers : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath: "AccessModifiers",
					apiDescriptionFile: "expected/AccessModifiers/AccessModifiers.xml",
					expectedRelativePath: "AccessModifiers",
					additionalSupportPaths: null);
		}
	}
}

