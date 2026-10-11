using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Streams : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "Streams",
					apiDescriptionFile:     "TestInputs/Streams/Streams.xml",
					expectedRelativePath:   "Streams",
					additionalSupportPaths: new[]{ "TestInputs/Streams/SupportFiles" });
		}
	}
}

