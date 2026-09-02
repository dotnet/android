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
			Options.UseUtf8MemberNames = true;
			RunAllTargets (
					outputRelativePath:     "StaticMethods",
					apiDescriptionFile:     "expected.ji/StaticMethods/StaticMethod.xml",
					expectedRelativePath:   "StaticMethods");
		}
	}
}
