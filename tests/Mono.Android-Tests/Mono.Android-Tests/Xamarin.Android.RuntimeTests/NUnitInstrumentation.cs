
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
                // Java.Interop-Tests fixtures that use JavaObject types (not Java.Lang.Object)
                // still need JCW Java classes or Java-side support that the trimmable typemap
                // path does not emit yet.
                // NOTE: Tests in this project that are trimmable-incompatible use
                // [Category("TrimmableIgnore")] so they can be excluded via ExcludeCategories in
                // the .csproj instead. Only tests from the external Java.Interop-Tests assembly
                // (which we don't control) need to be listed here by name.
                ExcludedTestNames = new [] {
                    "Java.InteropTests.JavaObjectTest",
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",
                    "Java.InteropTests.JniPeerMembersTests",
                    "Java.InteropTests.JniTypeManagerTests",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests",
                    "Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",

                    // JavaCast/JavaAs interface resolution still differs under trimmable typemap.
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_Exceptions",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull",

                    // JavaObjectArray<object> contract tests still need generic container factory support.
                    "Java.InteropTests.JavaObjectArray_object_ContractTest",
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
