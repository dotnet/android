using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	class JavaInteropCodeGeneratorTests : CodeGeneratorTests
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.JavaInterop1;
	}
}
