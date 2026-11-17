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
			RunTarget (
					outputRelativePath: "GenericArguments",
					apiDescriptionFile: "TestInputs/GenericArguments/GenericArguments.xml",
					expectedRelativePath: "GenericArguments",
					additionalSupportPaths: null);
		}
	}
}

