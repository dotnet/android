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
			RunTarget (
					outputRelativePath:     "NonStaticFields",
					apiDescriptionFile:     "TestInputs/NonStaticFields/NonStaticField.xml",
					expectedRelativePath:   "NonStaticFields");
		}
	}
}

