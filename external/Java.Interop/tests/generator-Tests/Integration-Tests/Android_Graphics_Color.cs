using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Android_Graphics_Color : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "Android.Graphics.Color",
					apiDescriptionFile:     "TestInputs/Android.Graphics.Color/Android.Graphics.Color.xml",
					expectedRelativePath:   "Android.Graphics.Color");
		}
	}
}

