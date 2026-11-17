using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class GenericArguments : BaseGeneratorTest
	{

		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath: "GenericArguments",
					apiDescriptionFile: "TestInputs/GenericArguments/GenericArguments.xml",
					expectedRelativePath: "GenericArguments",
					additionalSupportPaths: null);
		}
	}
}

