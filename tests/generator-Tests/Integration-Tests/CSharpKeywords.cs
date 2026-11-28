using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class CSharpKeywords : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "CSharpKeywords",
					apiDescriptionFile:     "TestInputs/CSharpKeywords/CSharpKeywords.xml",
					expectedRelativePath:   "CSharpKeywords");
		}
	}
}

