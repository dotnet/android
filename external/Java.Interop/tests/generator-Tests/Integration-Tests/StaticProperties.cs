using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class StaticProperties : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "StaticProperties",
					apiDescriptionFile:     "TestInputs/StaticProperties/StaticProperties.xml",
					expectedRelativePath:   "StaticProperties");
		}
	}
}

