
using System;
using System.Collections.Generic;
using System.Reflection;
using Android.App;
using Android.Runtime;
using Xamarin.Android.UnitTests;
using Xamarin.Android.UnitTests.NUnit;

namespace Xamarin.Android.RuntimeTests
{
    [Instrumentation (Name = "xamarin.android.runtimetests.NUnitInstrumentation", TargetPackage = "Mono.Android.NET_Tests")]
    public class NUnitInstrumentation : NUnitTestInstrumentation
    {
        const string DefaultLogTag = "NUnit";

        string logTag = DefaultLogTag;

        protected override string LogTag
        {
            get { return logTag; }
            set { logTag = value ?? DefaultLogTag; }
        }

        protected NUnitInstrumentation(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
			bool useTrimmableTypeMap = AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap", out bool trimmableTypeMap) && trimmableTypeMap;
			global::Android.Util.Log.Info (DefaultLogTag, $"Trimmable type map enabled: {useTrimmableTypeMap}");

			if (useTrimmableTypeMap) {
				ExcludedCategories = ["Export", "NativeTypeMap", "SSL", "TrimmableIgnore"];

				// Keep the temporary Java.Interop exclusions centralized here so
				// we don't need a PR against the Java.Interop submodule.
				ExcludedTestNames = new [] {
					"Java.InteropTests.JavaObjectTest",
					"Java.InteropTests.JavaObjectExtensionsTests",
					"Java.InteropTests.InvokeVirtualFromConstructorTests",
					"Java.InteropTests.JniPeerMembersTests",
					"Java.InteropTests.JniTypeManagerTests",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests",
					"Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",
					"Java.InteropTests.JavaPeerableExtensionsTests",
					"InvokeOverriddenAbsListView_AdapterProperty",
					"Xamarin.Android.RuntimeTests.AdapterTests.InvokeOverriddenAbsListView_AdapterProperty",
				};
			}
		}

        protected override IList<TestAssemblyInfo> GetTestAssemblies()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            Assembly ji  = typeof (Java.InteropTests.JavaInterop_Tests_Reference).Assembly;

            return new List<TestAssemblyInfo>()
            {
                new TestAssemblyInfo (asm, asm.Location ?? String.Empty),
                new TestAssemblyInfo (ji, ji.Location ?? String.Empty),
            };
        }
    }
}
