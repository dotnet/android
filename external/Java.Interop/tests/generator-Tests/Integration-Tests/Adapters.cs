using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Adapters : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "Adapters",
					apiDescriptionFile:     "expected.ji/Adapters/Adapters.xml",
					expectedRelativePath:   "Adapters",
					additionalSupportPaths: new[]{ "expected.ji/Adapters/SupportFiles" });
		}
	}
}

