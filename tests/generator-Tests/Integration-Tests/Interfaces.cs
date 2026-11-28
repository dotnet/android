using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Interfaces : BaseGeneratorTest
	{
		public Interfaces ()
		{
			// warning CS0108: 'IDeque.Add(Object)' hides inherited member 'IQueue.Add(Object)'. Use the new keyword if hiding was intended.
			// warning CS0108: 'IQueue.Add(Object)' hides inherited member 'ICollection.Add(Object)'. Use the new keyword if hiding was intended.
			AllowWarnings   = true;
		}


		[TestMethodAttribute]
		public void Generated_OK ()
		{
			RunTarget (
					outputRelativePath:     "TestInterface",
					apiDescriptionFile:     "TestInputs/TestInterface/TestInterface.xml",
					expectedRelativePath:   "TestInterface");
		}
	}
}

