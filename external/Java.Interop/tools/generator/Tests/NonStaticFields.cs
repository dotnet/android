using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NonStaticFields : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "NonStaticFields",
					apiDescriptionFile:     "expected/NonStaticFields/NonStaticField.xml",
					expectedRelativePath:   "NonStaticFields");
		}
	}
}

