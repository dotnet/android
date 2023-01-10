using System.Reflection;
using Xamarin.Android.NUnitLite;

namespace Xamarin.Android.BindingRuntime_Tests;

[Activity (Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : TestSuiteActivity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		// tests can be inside the main assembly
		AddTest (Assembly.GetExecutingAssembly ());

		// or in any reference assemblies
		// AddTest (typeof (Your.Library.TestClass).Assembly);

		// Once you called base.OnCreate(), you cannot add more assemblies.
		base.OnCreate (savedInstanceState);
	}
}
