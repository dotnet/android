
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
                // Tests excluded under the trimmable typemap path. Grouped by root cause:
                //
                // 1. Fixtures needing JCW/Java-side support the trimmable path doesn't emit yet.
                // 2. JavaCast/JavaAs, type-mapping, and activation gaps.
                // 3. SSL callback tests that SIGSEGV in libnet-android / ART GC (not a typemap bug).
                ExcludedTestNames = new [] {
                    // Whole fixtures that need JCW classes or JavaObject-level support
                    "Java.InteropTests.JavaObjectTest",
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",
                    "Java.InteropTests.JniPeerMembersTests",
                    "Java.InteropTests.JniTypeManagerTests",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests",
                    "Java.InteropTests.JavaObjectArray_object_ContractTest",

                    // Individual tests with typemap/activation/cast gaps
                    "Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_Exceptions",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BaseToGenericWrapper",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BadInterfaceCast",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_InvalidTypeCastThrows",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_CheckForManagedSubclasses",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaAs",
                    "Java.InteropTests.JnienvTest.NewOpenGenericTypeThrows",
                    "Java.InteropTests.JnienvTest.ActivatedDirectThrowableSubclassesShouldBeRegistered",
                    "Java.InteropTests.JnienvTest.ActivatedDirectObjectSubclassesShouldBeRegistered",
                    "Java.InteropTests.JnienvTest.CreateTypeWithExportedMethods",
                    "Java.InteropTests.JnienvTest.JavaToManagedTypeMapping",
                    "Java.InteropTests.JnienvTest.ManagedToJavaTypeMapping",
                    "Java.InteropTests.JnienvTest.DoNotLeakWeakReferences",

                    // SIGSEGV in ART GC during Runtime.gc() called by libnet-android (not a typemap bug).
                    // Affects any test that completes a successful SSL handshake and triggers GC.
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApproveRequest",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApprovesRequestWithInvalidCertificate",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_IgnoresCertificateHostnameMismatch",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_Redirects",
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
