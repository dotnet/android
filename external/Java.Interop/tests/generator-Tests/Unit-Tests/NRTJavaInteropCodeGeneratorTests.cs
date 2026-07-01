using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	class NRTJavaInteropCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;

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
