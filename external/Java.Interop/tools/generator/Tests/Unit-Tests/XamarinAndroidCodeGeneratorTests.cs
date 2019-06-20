using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	class XamarinAndroidCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XamarinAndroid;
	}
}
