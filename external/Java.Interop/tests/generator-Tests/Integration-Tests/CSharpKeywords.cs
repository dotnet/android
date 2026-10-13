using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class CSharpKeywords : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "CSharpKeywords",
					apiDescriptionFile:     "TestInputs/CSharpKeywords/CSharpKeywords.xml",
					expectedRelativePath:   "CSharpKeywords");
		}
	}
}

