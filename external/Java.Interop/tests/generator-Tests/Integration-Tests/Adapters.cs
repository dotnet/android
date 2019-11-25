using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Adapters : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "Adapters",
					apiDescriptionFile:     "expected/Adapters/Adapters.xml",
					expectedRelativePath:   "Adapters",
					additionalSupportPaths: new[]{ "expected/Adapters/SupportFiles" });
		}
	}
}

