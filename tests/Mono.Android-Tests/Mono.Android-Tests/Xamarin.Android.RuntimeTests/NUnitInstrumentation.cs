
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
                // trimmable typemap. These cannot use [Category("TrimmableIgnore")] because
                // we don't control that assembly — they must be excluded by name here.
                ExcludedTestNames = new [] {
                    // net.dot.jni.test.CallVirtualFromConstructorDerived Java class not in APK
                    "Java.InteropTests.InvokeVirtualFromConstructorTests",

                    // net.dot.jni.internal.JavaProxyObject.<clinit> calls
                    // net.dot.jni.ManagedPeer.registerNativeMembers, which the trimmable
                    // typemap path rejects (Native methods must be registered by JCW
                    // static initializer blocks). Fixing this requires a parallel
                    // Android-trimmable variant of JavaProxyObject.java that registers
                    // its native equals/hashCode/toString via mono.android.Runtime.register
                    // — an architectural change tracked separately from the JavaCast / JavaAs
                    // work in this PR. See https://github.com/dotnet/android/issues/11170.
                    "Java.InteropTests.JavaObjectArray_object_ContractTest",

                    // Same root cause as above (JavaProxyObject static init).
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericObjectReferenceArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericValue",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateObjectReferenceArgumentState",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateValue",
                    "Java.InteropTests.JniValueMarshaler_object_ContractTests.SpecificTypesAreUsed",

                    // No generated JavaPeerProxy for java/lang/Object with IJavaPeerable target type
                    // CreateGenericValue still fails because JavaArray<> proxy is selected for "java/lang/Object" lookup
                    // (alias resolution returns the open-generic JavaArray<> proxy whose CreateInstance throws).
                    // CreateValue now passes after generator fixes.
                    "Java.InteropTests.JniValueMarshaler_IJavaPeerable_ContractTests.JniValueMarshalerContractTests`1.CreateGenericValue",

                    // Trimmable typemap synthesizes peer construction via base activation ctor
                    // (EmitCreateInstanceInheritedJavaInteropCtor), so MyJavaInterfaceImpl can be cast
                    // even without its own activation ctor. This is intentionally more permissive than
                    // the legacy runtime, which throws NotSupportedException in this case.
                    // The other JavaAs tests pass with the trimmable typemap.
                    "Java.InteropTests.JavaPeerableExtensionsTests.JavaAs_Exceptions",

                    // JNI method remapping not supported in trimmable typemap
                    "Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodName",
                    "Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodWithStaticMethod",
                    "Java.InteropTests.JniPeerMembersTests.ReplacementTypeUsedForMethodLookup",
                    "Java.InteropTests.JniPeerMembersTests.ReplaceStaticMethodName",

                    // net.dot.jni.test.GenericHolder Java class not in APK
                    "Java.InteropTests.JniTypeManagerTests.CannotCreateGenericHolderFromJava",

                    // Open generic type handling differs from non-trimmable
                    "Java.InteropTests.JnienvTest.NewOpenGenericTypeThrows",

                    // net.dot.jni.internal.JavaProxyThrowable static init — same JavaProxy*
                    // root cause as the JavaProxyObject exclusions above.
                    "Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",

                    // Throwable subclass registration
                    "Java.InteropTests.JnienvTest.ActivatedDirectThrowableSubclassesShouldBeRegistered",

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
