using System;
using System.Collections.Generic;
using System.Reflection;
using Android.App;
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
				return value!.Split (new [] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			}
		}

		static bool HasAppContextSwitch (string key)
			=> AppContext.TryGetSwitch (key, out var value) && value;

		protected override IEnumerable<string>? ExcludedTestNames {
			get {
				if (!Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap)
					return null;

				// Tests from the external Java.Interop-Tests assembly that fail under the
				// trimmable typemap. These cannot use [Category] because we don't control
				// that assembly — they must be excluded by name here.
				return new [] {
					// Known limitation: [JniAddNativeMethodRegistrationAttribute] is not
					// supported by design under the trimmable typemap. This Java.Interop-Tests
					// fixture uses that attribute to register native callbacks on a hand-written
					// Java peer (an obsolete code path whose primary consumer, jnimarshalmethod-gen,
					// was removed in dotnet/java-interop#1405). The trimmable typemap generator
					// emits XA4251 when it encounters the attribute and instructs users to either
					// avoid it or switch off the trimmable typemap.
					// See https://github.com/dotnet/android/issues/11170.
					"Java.InteropTests.InvokeVirtualFromConstructorTests",
				};
			}
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
