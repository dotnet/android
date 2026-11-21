using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Streams : BaseGeneratorTest
	{
		[TestMethodAttribute]
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

