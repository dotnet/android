using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class AccessModifiers : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath: "AccessModifiers",
					apiDescriptionFile: "TestInputs/AccessModifiers/AccessModifiers.xml",
					expectedRelativePath: "AccessModifiers",
					additionalSupportPaths: null);
		}
	}
}

