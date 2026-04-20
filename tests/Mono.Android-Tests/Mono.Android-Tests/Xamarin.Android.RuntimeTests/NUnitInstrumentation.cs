
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

                    // These certificate-callback tests were narrowed down from the old broad SSL
                    // bucket. In Release+CoreCLR+trimmable runs they reproduce a native SIGSEGV
                    // before NUnit can record a result. The tombstone shows ART/CheckJNI with
                    // libnet-android.release.so calling into JNIEnv::CallVoidMethod.
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApproveRequest",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApprovesRequestWithInvalidCertificate",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_IgnoresCertificateHostnameMismatch",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_Redirects",

                    // JavaObjectArray<object> contract tests still need generic container factory support.
                    "Java.InteropTests.JavaObjectArray_object_ContractTest",

                    // Native typemap lookup and activation behavior still has a few trimmable-only gaps.
                    "Java.InteropTests.JnienvTest.NewOpenGenericTypeThrows",
                    "Java.InteropTests.JnienvTest.ActivatedDirectThrowableSubclassesShouldBeRegistered",
                    "Java.InteropTests.JnienvTest.JavaToManagedTypeMapping",
                    "Java.InteropTests.JnienvTest.ManagedToJavaTypeMapping",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BaseToGenericWrapper",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BadInterfaceCast",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_InvalidTypeCastThrows",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_CheckForManagedSubclasses",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaAs",
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
