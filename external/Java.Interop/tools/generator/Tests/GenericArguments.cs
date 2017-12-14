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
			//hides inherited member `Java.Lang.Object.class_ref'
			AllowWarnings = true;
			RunAllTargets (
					outputRelativePath: "GenericArguments",
					apiDescriptionFile: "expected/GenericArguments/GenericArguments.xml",
					expectedRelativePath: "GenericArguments",
					additionalSupportPaths: null);
		}
	}
}

