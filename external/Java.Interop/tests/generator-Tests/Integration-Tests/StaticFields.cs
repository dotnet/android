using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class StaticFields : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "StaticFields",
					apiDescriptionFile:     "expected.ji/StaticFields/StaticField.xml",
					expectedRelativePath:   "StaticFields");
		}
	}
}

