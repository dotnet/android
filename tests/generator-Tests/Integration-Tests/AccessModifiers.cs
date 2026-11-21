using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class AccessModifiers : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath: "AccessModifiers",
					apiDescriptionFile: "TestInputs/AccessModifiers/AccessModifiers.xml",
					expectedRelativePath: "AccessModifiers",
					additionalSupportPaths: null);
		}
	}
}

