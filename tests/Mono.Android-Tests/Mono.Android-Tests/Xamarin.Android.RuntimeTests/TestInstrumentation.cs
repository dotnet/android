using System;
using System.Collections.Generic;
using System.Reflection;
using Android.App;
using Android.Runtime;
using Xamarin.Android.UnitTests;

namespace Xamarin.Android.RuntimeTests
{
	[Instrumentation (Name = "xamarin.android.runtimetests.TestInstrumentation")]
	public class TestInstrumentation : Xamarin.Android.UnitTests.TestInstrumentation
	{
		protected TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override IEnumerable<string>? ExcludedCategories {
			get {
				var categories = new List<string> ();

				if (!Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime) {
					// CoreCLR-specific exclusions
					// TODO: https://github.com/dotnet/android/issues/10069
					categories.Add ("CoreCLRIgnore");
					categories.Add ("NTLM");
				}

				if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
					categories.Add ("NativeTypeMap");
					categories.Add ("Export");
				}

				return categories.Count > 0 ? categories : null;
			}
		}

		protected override IEnumerable<string>? ExcludedTestNames {
			get {
				if (!Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap)
					return null;

				// Tests from the external Java.Interop-Tests assembly that fail under the
				// trimmable typemap. These cannot use [Category] because we don't control
				// that assembly — they must be excluded by name here.
				return new [] {
					// net.dot.jni.test.CallVirtualFromConstructorDerived Java class not in APK
					"Java.InteropTests.InvokeVirtualFromConstructorTests",

					// net.dot.jni.internal.JavaProxyObject.<clinit> calls
					// net.dot.jni.ManagedPeer.registerNativeMembers, which the trimmable
					// typemap path rejects. See https://github.com/dotnet/android/issues/11170.
					"Java.InteropTests.JavaObjectArray_object_ContractTest",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateArgumentState",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericArgumentState",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericObjectReferenceArgumentState",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateGenericValue",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateObjectReferenceArgumentState",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests.JniValueMarshalerContractTests`1.CreateValue",
					"Java.InteropTests.JniValueMarshaler_object_ContractTests.SpecificTypesAreUsed",

					// net.dot.jni.test.GetThis static init — same JavaProxy* root cause
					"Java.InteropTests.JavaObjectTest.DisposeAccessesThis",

					// net.dot.jni.internal.JavaProxyThrowable static init — same root cause
					"Java.InteropTests.JavaExceptionTests.InnerExceptionIsNotAProxy",

					// JNI method remapping not supported in trimmable typemap
					"Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodName",
					"Java.InteropTests.JniPeerMembersTests.ReplaceInstanceMethodWithStaticMethod",
					"Java.InteropTests.JniPeerMembersTests.ReplacementTypeUsedForMethodLookup",
					"Java.InteropTests.JniPeerMembersTests.ReplaceStaticMethodName",

					// net.dot.jni.test.GenericHolder Java class not in APK
					"Java.InteropTests.JniTypeManagerTests.CannotCreateGenericHolderFromJava",

					// Open generic type handling differs from non-trimmable
					"Java.InteropTests.JnienvTest.NewOpenGenericTypeThrows",

					// Throwable subclass registration
					"Java.InteropTests.JnienvTest.ActivatedDirectThrowableSubclassesShouldBeRegistered",

					// Instance identity after JNI round-trip
					"Java.LangTests.ObjectTest.JnienvCreateInstance_RegistersMultipleInstances",

					// Global ref leak when inflating custom views
					"Xamarin.Android.RuntimeTests.CustomWidgetTests.InflateCustomView_ShouldNotLeakGlobalRefs",
				};
			}
		}

		protected override IEnumerable<Assembly> GetTestAssemblies ()
		{
			return [
				Assembly.GetExecutingAssembly (),
				typeof (Java.InteropTests.JavaInterop_Tests_Reference).Assembly,
			];
		}
	}
}
