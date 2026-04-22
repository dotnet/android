
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
                    // JCW Java class not in APK (0/3 pass)
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",

                    // JCW Java class not in APK (fixture setup fails, 0/16 pass)
                    "Java.InteropTests.JavaObjectArray_object_ContractTest",

                    // JCW Java class not in APK: JavaProxyObject
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericObjectReferenceArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericValue",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateObjectReferenceArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateValue",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.SpecificTypesAreUsed",

                    // JCW Java class not in APK: JavaProxyThrowable
                    "Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",

                    // MissingMethodException: IJavaInterfaceInvoker ctor trimmed
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs",
                    // Wrong exception type (ClassNotFoundException vs ArgumentException)
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_Exceptions",
                    // No generated JavaPeerProxy for IAndroidInterface
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull",

                    // JNI method remapping not supported in trimmable typemap
                    "Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodName",
                    "Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodWithStaticMethod",
                    "Java.InteropTests.JniPeerMembersTests.ReplacementTypeUsedForMethodLookup",
                    "Java.InteropTests.JniPeerMembersTests.ReplaceStaticMethodName",

                    // Java class GenericHolder not in DEX
                    "Java.InteropTests.JniTypeManagerTests.CanCreateGenericHolder",
                    "Java.InteropTests.JniTypeManagerTests.CannotCreateGenericHolderFromJava",
                    // JniPrimitiveArrayInfo lookup fails
                    "Java.InteropTests.JniTypeManagerTests.GetType",
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
