using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Core_ClassParse : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			AllowWarnings = true;

			RunAllTargets (
					outputRelativePath: "Core_ClassParse",
					apiDescriptionFile: "expected/Core_ClassParse/api.xml",
					expectedRelativePath: "Core_ClassParse");
		}
	}
}

