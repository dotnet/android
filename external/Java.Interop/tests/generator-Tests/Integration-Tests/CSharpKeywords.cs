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
			RunAllTargets (
					outputRelativePath:     "CSharpKeywords",
					apiDescriptionFile:     "expected/CSharpKeywords/CSharpKeywords.xml",
					expectedRelativePath:   "CSharpKeywords");
		}
	}
}

