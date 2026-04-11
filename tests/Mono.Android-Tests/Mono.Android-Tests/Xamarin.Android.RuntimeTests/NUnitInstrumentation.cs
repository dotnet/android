
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
            if (AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap", out bool trimmableTypeMap) && trimmableTypeMap) {
                var excludedCategories = new List<string> {
                    "Export",
                    "GCBridge",
                    "NativeTypeMap",
                    "SSL",
                    "TrimmableIgnore",
                };
                if (AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime", out bool isCoreClrRuntime) && isCoreClrRuntime) {
                    excludedCategories.Add ("CoreCLRIgnore");
                }
                ExcludedCategories = excludedCategories;

                // Java.Interop-Tests fixtures that use JavaObject types (not Java.Lang.Object)
                // don't have JCW Java classes in the trimmable APK, and method remapping
                // tests require Java-side support not present in the trimmable path.
                // Keep short simple names alongside fully-qualified names because the
                // instrumentation filter matches both individual tests and fixtures.
                ExcludedTestNames = new [] {
                    "JavaObjectTest",
                    "Java.InteropTests.JavaObjectTest",
                    "JavaObjectExtensionsTests",
                    "Java.InteropTests.JavaObjectExtensionsTests",
                    "InvokeVirtualFromConstructorTests",
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",
                    "JniPeerMembersTests",
                    "Java.InteropTests.JniPeerMembersTests",
                    "JniTypeManagerTests",
                    "Java.InteropTests.JniTypeManagerTests",
                    "JniValueMarshaler_object_ContractTests",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests",
                    "InnerExceptionIsNotAProxy",
                    "Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",
                    "JavaPeerableExtensionsTests",
                    "JavaAs",
                    "JavaAs_Exceptions",
                    "JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull",
                    "Java.InteropTests.JavaPeerableExtensionsTests",
                    "CreateTypeWithExportedMethods",
                    "Java.InteropTests.JnienvTest.CreateTypeWithExportedMethods",
                    "DoNotLeakWeakReferences",
                    "Java.InteropTests.JnienvTest.DoNotLeakWeakReferences",
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
