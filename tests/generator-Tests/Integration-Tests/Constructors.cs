using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Constructors : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "Constructors",
					apiDescriptionFile:     "TestInputs/Constructors/Constructors.xml",
					expectedRelativePath:   "Constructors");
		}
	}
}

