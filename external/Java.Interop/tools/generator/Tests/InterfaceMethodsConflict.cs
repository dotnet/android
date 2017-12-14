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
			//hides inherited member `Java.Lang.Object.class_ref'
			AllowWarnings = true;
			RunAllTargets (
					outputRelativePath:     "InterfaceMethodsConflict",
					apiDescriptionFile:     "expected/InterfaceMethodsConflict/InterfaceMethodsConflict.xml",
					expectedRelativePath:   "InterfaceMethodsConflict");
		}
	}
}

