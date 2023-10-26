using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Interfaces : BaseGeneratorTest
	{
		public Interfaces ()
		{
			// warning CS0108: 'IDeque.Add(Object)' hides inherited member 'IQueue.Add(Object)'. Use the new keyword if hiding was intended.
			// warning CS0108: 'IQueue.Add(Object)' hides inherited member 'ICollection.Add(Object)'. Use the new keyword if hiding was intended.
			AllowWarnings   = true;
		}

		protected override bool TryJavaInterop1 => true;

		[Test]
		public void Generated_OK ()
		{
			RunAllTargets (
					outputRelativePath:     "TestInterface",
					apiDescriptionFile:     "expected.ji/TestInterface/TestInterface.xml",
					expectedRelativePath:   "TestInterface");
		}
	}
}

