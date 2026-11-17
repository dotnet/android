using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Android_Graphics_Color : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "Android.Graphics.Color",
					apiDescriptionFile:     "TestInputs/Android.Graphics.Color/Android.Graphics.Color.xml",
					expectedRelativePath:   "Android.Graphics.Color");
		}
	}
}

