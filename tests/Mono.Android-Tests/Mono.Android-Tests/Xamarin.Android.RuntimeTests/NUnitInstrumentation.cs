
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
                var excludedTests = new Dictionary<string, string> (StringComparer.Ordinal) {
                    // Java.Interop-Tests fixtures that use JavaObject types (not Java.Lang.Object)
                    // don't have JCW Java classes in the trimmable APK, and method remapping
                    // tests require Java-side support not present in the trimmable path.
                    ["Java.InteropTests.JavaObjectTest"] = "JavaObject-based fixtures need JCW Java classes that are not emitted in the trimmable typemap APK yet.",
                    ["Java.InteropTests.InvokeVirtualFromConstructorTests"] = "Virtual dispatch from Java-side constructors is not fully supported in the trimmable typemap path yet.",
                    ["Java.InteropTests.JniPeerMembersTests"] = "JniPeerMembers registration still depends on Java-side support that the trimmable typemap path does not provide yet.",
                    ["Java.InteropTests.JniTypeManagerTests"] = "TypeManager contract tests still depend on Java-side registration behavior that is incomplete for trimmable typemap.",
                    ["Java.InteropTests.JniValueMarshaler_object_ContractTests"] = "JavaObject marshaling contract tests need object-container support that is incomplete in the trimmable typemap path.",
                    ["Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy"] = "Exception proxying is not yet preserved consistently in the trimmable typemap path.",

                    // JavaCast/JavaAs interface resolution is not yet supported under trimmable typemap.
                    ["Java.InteropTests.JavaPeerableExtensionsTests.JavaAs"] = "JavaAs interface resolution is not yet supported in the trimmable typemap path.",
                    ["Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_Exceptions"] = "JavaAs exception behavior is not yet supported in the trimmable typemap path.",
                    ["Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull"] = "JavaAs interface resolution is not yet supported in the trimmable typemap path.",

                    // In Release+CoreCLR+trimmable device runs, these certificate-callback tests
                    // reproduce a native SIGSEGV as soon as they enter the validation path. The
                    // tombstone shows ART/CheckJNI on the stack with libnet-android.release.so
                    // calling into JNIEnv::CallVoidMethod, and NUnit never gets a result back.
                    ["Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApproveRequest"] = "This test currently crashes the app with SIGSEGV in Release CoreCLR trimmable runs.",
                    ["Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_ApprovesRequestWithInvalidCertificate"] = "This test currently crashes the app with SIGSEGV in Release CoreCLR trimmable runs.",
                    ["Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_IgnoresCertificateHostnameMismatch"] = "This test currently crashes the app with SIGSEGV in Release CoreCLR trimmable runs.",
                    ["Xamarin.Android.NetTests.AndroidMessageHandlerTests.ServerCertificateCustomValidationCallback_Redirects"] = "This test currently crashes the app with SIGSEGV in Release CoreCLR trimmable runs.",

                    // JavaObjectArray<object> contract tests need generic container factory support.
                    ["Java.InteropTests.JavaObjectArray_object_ContractTest"] = "JavaObjectArray<object> contract tests need generic container factory support in the trimmable typemap path.",

                    // Mono.Android.NET-Tests currently excluded by broad categories can be narrowed to explicit reasons.
                    ["Java.InteropTests.JnienvTest.NewOpenGenericTypeThrows"] = "Open generic Java activation still needs dedicated trimmable typemap support.",
                    ["Java.InteropTests.JnienvTest.ActivatedDirectThrowableSubclassesShouldBeRegistered"] = "Throwable activation from Java is not yet fully registered in the trimmable typemap path.",
                    ["Java.InteropTests.JnienvTest.JavaToManagedTypeMapping"] = "Native typemap lookup APIs are not applicable when _AndroidTypeMapImplementation=trimmable.",
                    ["Java.InteropTests.JnienvTest.ManagedToJavaTypeMapping"] = "Native typemap lookup APIs are not applicable when _AndroidTypeMapImplementation=trimmable.",
                    ["Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BaseToGenericWrapper"] = "JavaCast generic wrapper support is not yet complete in the trimmable typemap path.",
                    ["Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BadInterfaceCast"] = "JavaCast interface failure behavior still differs under the trimmable typemap path.",
                    ["Java.InteropTests.JavaObjectExtensionsTests.JavaCast_InvalidTypeCastThrows"] = "JavaCast invalid-type behavior still differs under the trimmable typemap path.",
                    ["Java.InteropTests.JavaObjectExtensionsTests.JavaCast_CheckForManagedSubclasses"] = "Managed-subclass JavaCast checks are not yet supported in the trimmable typemap path.",
                    ["Java.InteropTests.JavaObjectExtensionsTests.JavaAs"] = "JavaAs interface resolution is not yet supported in the trimmable typemap path.",
                };

                ExcludedTestNames = excludedTests.Keys;
                ExcludedTestReasons = excludedTests;
                ExcludedCategoryReasons = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
                    ["NativeTypeMap"] = "Native typemap lookup tests are not yet supported when the trimmable typemap implementation is enabled.",
                    ["TrimmableIgnore"] = "Known trimmable typemap gaps are kept excluded until each test can be fixed or re-enabled individually.",
                    ["SSL"] = "Most SSL/network coverage is now enabled again; only a small set of named certificate-callback SIGSEGV tests remains excluded.",
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
