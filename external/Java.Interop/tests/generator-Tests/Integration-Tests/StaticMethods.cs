using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class StaticMethods : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "StaticMethods",
					apiDescriptionFile:     "TestInputs/StaticMethods/StaticMethod.xml",
					expectedRelativePath:   "StaticMethods");
		}
	}
}

