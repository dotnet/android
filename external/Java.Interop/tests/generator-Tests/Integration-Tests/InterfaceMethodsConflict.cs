using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class InterfaceMethodsConflict : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "InterfaceMethodsConflict",
					apiDescriptionFile:     "TestInputs/InterfaceMethodsConflict/InterfaceMethodsConflict.xml",
					expectedRelativePath:   "InterfaceMethodsConflict");
		}
	}
}

