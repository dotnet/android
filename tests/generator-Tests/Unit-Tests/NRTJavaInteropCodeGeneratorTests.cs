using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	class NRTJavaInteropCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationOptions CreateOptions ()
		{
			var options = base.CreateOptions ();

			options.SupportNullableReferenceTypes = true;

			return options;
		}

		protected override string CommonDirectoryOverride => "XAJavaInterop1-NRT";
		protected override string TargetedDirectoryOverride => "XAJavaInterop1-NRT";
	}
}
