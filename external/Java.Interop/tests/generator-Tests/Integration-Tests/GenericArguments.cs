using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class GenericArguments : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath: "GenericArguments",
					apiDescriptionFile: "expected.ji/GenericArguments/GenericArguments.xml",
					expectedRelativePath: "GenericArguments",
					additionalSupportPaths: null);
		}
	}
}

