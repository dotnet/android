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
			RunAllTargets (
					outputRelativePath:     "InterfaceMethodsConflict",
					apiDescriptionFile:     "expected/InterfaceMethodsConflict/InterfaceMethodsConflict.xml",
					expectedRelativePath:   "InterfaceMethodsConflict");
		}
	}
}

