using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NormalMethods : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			//`Xamarin.Test.SomeObject.GetType()' hides inherited member `object.GetType()'
			//`Xamarin.Test.SomeObject.ObsoleteMethod()' is obsolete
			AllowWarnings = true;
			RunAllTargets (
					outputRelativePath:     "NormalMethods",
					apiDescriptionFile:     "expected/NormalMethods/NormalMethods.xml",
					expectedRelativePath:   "NormalMethods");
		}
	}
}

