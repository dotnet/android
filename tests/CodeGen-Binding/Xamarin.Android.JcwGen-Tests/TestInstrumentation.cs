using System.Reflection;
using Android.App;
using Android.Runtime;
using Xamarin.Android.UnitTests;

[assembly: Instrumentation (
	Name = "xamarin.android.jcwgentests.TestInstrumentation",
	TargetPackage = "Xamarin.Android.JcwGen_Tests"
)]

namespace Xamarin.Android.JcwGenTests
{
	[Instrumentation (Name = "xamarin.android.jcwgentests.TestInstrumentation")]
	public class TestInstrumentation : Xamarin.Android.UnitTests.TestInstrumentation
	{
		public TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override IEnumerable<Assembly> GetTestAssemblies ()
		{
			return [Assembly.GetExecutingAssembly ()];
		}
	}
}
