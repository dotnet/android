
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
                // don't have JCW Java classes in the trimmable APK yet, and a few remaining
                // JavaCast / typemap / SSL callback cases still reproduce concrete failures.
                ExcludedTestNames = new [] {
                    "Java.InteropTests.JavaObjectTest",
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",
                    "Java.InteropTests.JniPeerMembersTests",
                    "Java.InteropTests.JniTypeManagerTests",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests",
                    "Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_Exceptions",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApproveRequest",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApprovesRequestWithInvalidCertificate",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_IgnoresCertificateHostnameMismatch",
                    "Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_Redirects",
                    "Java.InteropTests.JavaObjectArray_object_ContractTest",
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

                ExcludedCategories = new [] {
                    "NativeTypeMap",
                    "TrimmableIgnore",
                    "SSL",
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
