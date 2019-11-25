using System;
using System.IO;
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
			Options.ApiDescriptionFile = FullPath ("expected/EnumerationFixup/EnumerationFixup.xml");
			Options.EnumFieldsMapFile = FullPath ("expected/EnumerationFixup/EnumerationFixupMap.xml");
			Options.EnumOutputDirectory = Options.ManagedCallableWrapperSourceOutputDirectory = FullPath ("out/EnumerationFixup");
			Options.EnumMetadataOutputFile = Path.Combine (Options.EnumOutputDirectory, "enummetadata");
			Execute ();
			Options.EnumFieldsMapFile = "";
			Options.EnumOutputDirectory = "";
			CompareOutputs ("expected/EnumerationFixup", "out/EnumerationFixup");
		}

	}
}

