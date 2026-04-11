
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

                // TODO: https://github.com/dotnet/android/issues/11170
                // Tests from the external Java.Interop-Tests assembly that fail under the
                // trimmable typemap. These cannot use [Category("TrimmableIgnore")] because
                // we don't control that assembly — they must be excluded by name here.
                // Keep short simple names alongside fully-qualified names because the
                // instrumentation filter matches both individual tests and fixtures.
                ExcludedTestNames = new [] {
                    "JavaObjectTest",
                    // net.dot.jni.test.CallVirtualFromConstructorDerived Java class not in APK
                    "Java.InteropTests.JavaObjectTest",
                    "JavaObjectExtensionsTests",
                    "Java.InteropTests.JavaObjectExtensionsTests",
                    "InvokeVirtualFromConstructorTests",
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",

                    // net.dot.jni.internal.JavaProxyObject Java class not in APK — fixture setup fails (16 tests)
                    "Java.InteropTests.JavaObjectArray_object_ContractTest",

                    // net.dot.jni.internal.JavaProxyObject Java class not in APK
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericObjectReferenceArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericValue",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateObjectReferenceArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateValue",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.SpecificTypesAreUsed",

                    // No generated JavaPeerProxy for java/lang/Object with IJavaPeerable target type
                    "Java.InteropTests.JniValueMarshaler_IJavaPeerable_ContractTests.JniValueMarshalerContractTests`1.CreateGenericValue",
                    "Java.InteropTests.JniValueMarshaler_IJavaPeerable_ContractTests.JniValueMarshalerContractTests`1.CreateValue",

                    // net.dot.jni.internal.JavaProxyThrowable — proxy throwable creation fails
                    "InnerExceptionIsNotAProxy",
                    "Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",

                    // IJavaInterfaceInvoker ctor trimmed / missing JavaPeerProxy for test types
                    "JavaPeerableExtensionsTests",
                    "JavaAs",
                    "JavaAs_Exceptions",
                    "JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull",
                    "Java.InteropTests.JavaPeerableExtensionsTests",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_Exceptions",
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull",

                    // JNI method remapping not supported in trimmable typemap
                    "JniPeerMembersTests",
                    "Java.InteropTests.JniPeerMembersTests",
                    "Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodName",
                    "Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodWithStaticMethod",
                    "Java.InteropTests.JniPeerMembersTests.ReplacementTypeUsedForMethodLookup",
                    "Java.InteropTests.JniPeerMembersTests.ReplaceStaticMethodName",

                    // net.dot.jni.test.GenericHolder Java class not in APK
                    "JniTypeManagerTests",
                    "Java.InteropTests.JniTypeManagerTests",
                    "Java.InteropTests.JniTypeManagerTests.CannotCreateGenericHolderFromJava",

                    // JniPrimitiveArrayInfo lookup fails for JavaBooleanArray
                    "Java.InteropTests.JniTypeManagerTests.GetType",

                    // net.dot.jni.test.GetThis — cannot register native members
                    "Java.InteropTests.JavaObjectTest.DisposeAccessesThis",

                    // NotSupportedException instead of InvalidCastException — no generated JavaPeerProxy
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BadInterfaceCast",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_BaseToGenericWrapper",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_CheckForManagedSubclasses",
                    "Java.InteropTests.JavaObjectExtensionsTests.JavaCast_InvalidTypeCastThrows",

                    // Open generic type handling differs from non-trimmable
                    "Java.InteropTests.JnienvTest.NewOpenGenericTypeThrows",

                    // Throwable subclass registration
                    "Java.InteropTests.JnienvTest.ActivatedDirectThrowableSubclassesShouldBeRegistered",

                    // Export attribute not supported in trimmable typemap
                    "CreateTypeWithExportedMethods",
                    "Java.InteropTests.JnienvTest.CreateTypeWithExportedMethods",
                    "DoNotLeakWeakReferences",
                    "Java.InteropTests.JnienvTest.DoNotLeakWeakReferences",

                    // Typemap doesn't resolve most-derived type
                    "Java.LangTests.ObjectTest.GetObject_ReturnsMostDerivedType",

                    // Instance identity after JNI round-trip
                    "Java.LangTests.ObjectTest.JnienvCreateInstance_RegistersMultipleInstances",

                    // Global ref leak when inflating custom views
                    "Xamarin.Android.RuntimeTests.CustomWidgetTests.InflateCustomView_ShouldNotLeakGlobalRefs",
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
