
using System;
using System.Collections.Generic;
using System.Reflection;
using Android.App;
using Android.Runtime;
using Xamarin.Android.UnitTests;
using Xamarin.Android.UnitTests.NUnit;

namespace Xamarin.Android.RuntimeTests
{
    [Instrumentation(Name = "xamarin.android.runtimetests.NUnitInstrumentation")]
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
            if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
                // TODO: https://github.com/dotnet/android/issues/11170
                // Tests from the external Java.Interop-Tests assembly that fail under the
                // trimmable typemap. We don't control that assembly, so they must be
                // excluded by name here.
                ExcludedTestNames = new [] {
                    // Known limitation: [JniAddNativeMethodRegistrationAttribute] is not
                    // supported by design under the trimmable typemap. This Java.Interop-Tests
                    // fixture uses that attribute
                    // to register native callbacks on a hand-written Java peer (an obsolete code
                    // path whose primary consumer, jnimarshalmethod-gen, was removed in
                    // dotnet/java-interop#1405). The trimmable typemap generator emits XA4251
                    // when it encounters the attribute and instructs users to either avoid it or
                    // switch off the trimmable typemap. See https://github.com/dotnet/android/issues/11170.
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",
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
