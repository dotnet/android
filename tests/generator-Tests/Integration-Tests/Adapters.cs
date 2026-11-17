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
			RunTarget (
					outputRelativePath:     "Adapters",
					apiDescriptionFile:     "TestInputs/Adapters/Adapters.xml",
					expectedRelativePath:   "Adapters",
					additionalSupportPaths: new[]{ "TestInputs/Adapters/SupportFiles" });
		}
	}
}

