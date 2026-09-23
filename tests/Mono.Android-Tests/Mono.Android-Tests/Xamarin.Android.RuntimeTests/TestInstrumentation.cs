using System;
using System.Collections.Generic;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Runtime;
using Xamarin.Android.UnitTests;

namespace Xamarin.Android.RuntimeTests
{
	[Instrumentation (Name = "xamarin.android.runtimetests.TestInstrumentation")]
	public class TestInstrumentation : Xamarin.Android.UnitTests.TestInstrumentation
	{
		const string JniReferenceLeakCategory = "JniReferenceLeak";

		protected TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override IEnumerable<string>? ExcludedCategories {
			get {
				var categories = new List<string> ();

				// CoreCLR-specific exclusions
				// TODO: https://github.com/dotnet/android/issues/10069
				categories.Add ("CoreCLRIgnore");
				categories.Add ("NTLM");

				// Build-time flags flow in via runtimeconfig.json properties
				// (see <RuntimeHostConfigurationOption> entries in Mono.Android.NET-Tests.csproj).
				if (HasAppContextSwitch ("PublishAot")) {
					// TODO: https://github.com/dotnet/android/issues/10079
					categories.Add ("NativeAOTIgnore");
					categories.Add ("SSL");
					categories.Add ("NTLM");
				}

				// Process-wide reference counts are only stable in the dedicated filtered run.
				if (!IsOnlyIncludedCategory (JniReferenceLeakCategory)) {
					categories.Add (JniReferenceLeakCategory);
				}

				return categories.Count > 0 ? categories : null;
			}
		}

		protected override IEnumerable<string>? IncludedCategories {
			get {
				// Wired up from the MSBuild $(IncludeCategories) pipeline property via a
				// `<RuntimeHostConfigurationOption Include="IncludeCategories" Value="..." />`
				// entry in the test csproj. The SDK writes that into runtimeconfig.json's
				// `configProperties` section, and we read it back with `AppContext.GetData`.
				// Used by lanes that want to scope a run to specific categories, e.g.
				// `-p:IncludeCategories=Intune` in stage-package-tests.yaml.
				var categories = GetIncludedCategories ();
				return categories.Length > 0 ? categories : null;
			}
		}

		static bool IsOnlyIncludedCategory (string category)
		{
			var categories = GetIncludedCategories ();
			return categories.Length == 1 && string.Equals (categories [0], category, StringComparison.Ordinal);
		}

		static string [] GetIncludedCategories ()
		{
			var value = AppContext.GetData ("IncludeCategories") as string;
			if (value == null)
				return [];
			return value.Split (new [] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		}

		static bool HasAppContextSwitch (string key)
			=> AppContext.TryGetSwitch (key, out var value) && value;

		public override void OnCreate (Bundle? arguments)
		{
			Java.Lang.JavaSystem.LoadLibrary ("reuse-threads");
			base.OnCreate (arguments);
		}

		protected override IEnumerable<Assembly> GetTestAssemblies ()
		{
			return [
				Assembly.GetExecutingAssembly (),
				typeof (Java.InteropTests.JavaInterop_Tests_Reference).Assembly,
			];
		}
	}
}
