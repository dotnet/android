using System.Reflection;
using Android.App;
using Android.Runtime;
using Xamarin.Android.UnitTests;

namespace Xamarin.Android.JcwGenTests
{
	[Instrumentation (Name = "xamarin.android.jcwgentests.TestInstrumentation")]
	public class TestInstrumentation : Xamarin.Android.UnitTests.TestInstrumentation
	{
		public TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override IEnumerable<string>? IncludedCategories {
			get {
				var value = AppContext.GetData ("IncludeCategories") as string;
				if (string.IsNullOrWhiteSpace (value))
					return null;

				var categories = value.Split (new [] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				return categories.Length > 0 ? categories : null;
			}
		}

		protected override IEnumerable<Assembly> GetTestAssemblies ()
		{
			return [Assembly.GetExecutingAssembly ()];
		}
	}
}
