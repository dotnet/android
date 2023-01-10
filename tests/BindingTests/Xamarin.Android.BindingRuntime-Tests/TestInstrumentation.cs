using System.Reflection;
using Android.Runtime;
using Xamarin.Android.NUnitLite;

namespace Xamarin.Android.BindingRuntime_Tests
{

	[Instrumentation (Name = "xamarin.android.bindingruntime.TestInstrumentation")]
	public class TestInstrumentation : TestSuiteInstrumentation
	{

		public TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override void AddTests ()
		{
			AddTest (Assembly.GetExecutingAssembly ());
		}
	}
}

