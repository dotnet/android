using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NestedTypes : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			//hides inherited member `Java.Lang.Object.class_ref'
			AllowWarnings = true;
			RunAllTargets (
					outputRelativePath:     "NestedTypes",
					apiDescriptionFile:     "expected/NestedTypes/NestedTypes.xml",
					expectedRelativePath:   "NestedTypes");
		}
	}
}
