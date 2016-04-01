using System;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Enumerations : BaseGeneratorTest
	{
		[Test]
		public void FixedUp_OK ()
		{
			Cleanup ("out/EnumerationFixup");
			Options.ApiDescriptionFile = "expected/EnumerationFixup/EnumerationFixup.xml";
			Options.EnumFieldsMapFile = "expected/EnumerationFixup/EnumerationFixupMap.xml";
			Options.EnumOutputDirectory = Options.ManagedCallableWrapperSourceOutputDirectory = "out/EnumerationFixup";
			Execute ();
			Options.EnumFieldsMapFile = "";
			Options.EnumOutputDirectory = "";
			CompareOutputs ("expected/EnumerationFixup", "out/EnumerationFixup");
		}

	}
}

