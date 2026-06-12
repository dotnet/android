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
		protected TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override IEnumerable<string>? ExcludedCategories {
			get {
				var categories = new List<string> ();

				if (!Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime) {
					// CoreCLR-specific exclusions
					// TODO: https://github.com/dotnet/android/issues/10069
					categories.Add ("CoreCLRIgnore");
					categories.Add ("NTLM");
				}

				if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
					categories.Add ("NativeTypeMap");
					categories.Add ("Export");
					categories.Add ("TrimmableTypeMapUnsupported");
				}

				// Build-time flags flow in via runtimeconfig.json properties
				// (see <RuntimeHostConfigurationOption> entries in Mono.Android.NET-Tests.csproj).
				if (HasAppContextSwitch ("PublishAot")) {
					// TODO: https://github.com/dotnet/android/issues/10079
					categories.Add ("NativeAOTIgnore");
					categories.Add ("SSL");
					categories.Add ("NTLM");
					categories.Add ("Export");
				}

				if (HasAppContextSwitch ("EnableLLVM")) {
					// FIXME: LLVMIgnore https://github.com/dotnet/runtime/issues/89190
					categories.Add ("LLVMIgnore");
					// InetAccess: https://github.com/dotnet/runtime/issues/73304
					categories.Add ("InetAccess");
					// NetworkInterfaces: https://github.com/dotnet/runtime/issues/75155
					categories.Add ("NetworkInterfaces");
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
				var value = AppContext.GetData ("IncludeCategories") as string;
				if (string.IsNullOrEmpty (value))
					return null;
				return value.Split (new [] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			}
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
