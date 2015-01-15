using System.Reflection;

using Android.App;
using Android.OS;

using Xamarin.Android.NUnitLite;

using Java.Interop;

namespace Android.InteropTests {
	[Activity (Label = "Android.Interop-Tests", MainLauncher = true)]
	public class MainActivity : TestSuiteActivity {

		#pragma warning disable 0414
		static readonly JavaVM current  = AndroidVM.Current;
		#pragma warning restore 0414

		protected override void OnCreate (Bundle bundle)
		{
			// tests can be inside the main assembly
			AddTest (Assembly.GetExecutingAssembly ());
			// or in any reference assemblies
			// AddTest (typeof (Your.Library.TestClass).Assembly);

			// Once you called base.OnCreate(), you cannot add more assemblies.
			base.OnCreate (bundle);
		}
	}
}

