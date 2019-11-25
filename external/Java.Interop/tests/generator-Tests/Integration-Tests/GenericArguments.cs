using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class GenericArguments : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath: "GenericArguments",
					apiDescriptionFile: "expected/GenericArguments/GenericArguments.xml",
					expectedRelativePath: "GenericArguments",
					additionalSupportPaths: null);
		}
	}
}

