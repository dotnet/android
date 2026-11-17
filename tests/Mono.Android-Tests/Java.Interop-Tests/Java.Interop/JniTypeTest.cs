using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JniTypeTest {

		[TestMethod]
		public unsafe void Sanity ()
		{
			using (var Integer_class = new JniType ("java/lang/Integer")) {
				Assert.AreEqual ("java/lang/Integer", Integer_class.Name);

				var ctor_args = stackalloc JniArgumentValue [1];
				ctor_args [0] = new JniArgumentValue (42);

				var Integer_ctor        = Integer_class.GetConstructor ("(I)V");
				var Integer_intValue    = Integer_class.GetInstanceMethod ("intValue", "()I");
				var o                   = Integer_class.NewObject (Integer_ctor, ctor_args);
				try {
					int v = JniEnvironment.InstanceMethods.CallIntMethod (o, Integer_intValue);
					Assert.AreEqual (42, v);
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}

		[TestMethod]
		public void Ctor_ThrowsIfTypeNotFound ()
		{
			Assert.ThrowsExactly<Java.Lang.ClassNotFoundException> (() => new JniType ("__this__/__type__/__had__/__better__/__not__/__Exist__")).Dispose ();
		}

		[TestMethod]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Test exercises non-AOT-compatible JniType.RegisterNativeMethods API.")]
		public unsafe void Dispose_Exceptions ()
		{
			var t = new JniType ("java/lang/Object");
			t.Dispose ();
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.AllocObject ());
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.NewObject (null, null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetConstructor ((string) null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetInstanceField ((string) null, (string) null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetInstanceMethod ((string) null, (string) null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetStaticField ((string) null, (string) null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetStaticMethod ((string) null, (string) null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetSuperclass ());
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.IsAssignableFrom (null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.IsInstanceOfType (new JniObjectReference ()));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.RegisterWithRuntime ());
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.RegisterNativeMethods (null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.UnregisterNativeMethods ());

			JniFieldInfo    jif = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetCachedInstanceField (ref jif, (string) null, (string) null));
			JniMethodInfo   jim = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetCachedConstructor (ref jim, (string) null));
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetCachedInstanceMethod (ref jim, (string) null, (string) null));
			JniFieldInfo    jsf = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetCachedStaticField (ref jsf, (string) null, (string) null));
			JniMethodInfo   jsm = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => t.GetCachedStaticMethod (ref jsm, (string) null, (string) null));
		}

		[TestMethod]
		public void GetSuperclass ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var b = t.GetSuperclass ();
				Assert.IsNull (b);
				using (var s = new JniType ("java/lang/String")) {
					using (var st = s.GetSuperclass ()) {
						Assert.IsFalse (object.ReferenceEquals (t, st));
						Assert.IsTrue (JniEnvironment.Types.IsSameObject (t.PeerReference, st.PeerReference));
					}
				}
			}
		}

		[TestMethod]
		public void IsAssignableFrom ()
		{
			using (var o = new JniType ("java/lang/Object"))
			using (var s = new JniType ("java/lang/String")) {
				Assert.IsTrue (o.IsAssignableFrom (s));
				Assert.IsFalse (s.IsAssignableFrom (o));
			}
		}

		[TestMethod]
		public unsafe void IsInstanceOfType ()
		{
			using (var Object_class = new JniType ("java/lang/Object"))
			using (var String_class = new JniType ("java/lang/String")) {
				var String_ctor = String_class.GetConstructor ("()V");
				var s           = String_class.NewObject (String_ctor, null);
				try {
					Assert.IsTrue (Object_class.IsInstanceOfType (s), "java.lang.String IS-NOT-A java.lang.Object?!");
				} finally {
					JniObjectReference.Dispose (ref s);
				}
			}
		}

		[TestMethod]
		public void InvalidSignatureThrowsJniException ()
		{
			using (var Integer_class = new JniType ("java/lang/Integer")) {
				Assert.ThrowsExactly<Java.Lang.NoSuchMethodError> (() => Integer_class.GetConstructor ("(C)V")).Dispose ();
			}
		}

		[TestMethod]
		public void GetStaticFieldID ()
		{
			using (var System_class = new JniType ("java/lang/System")) {
				var System_in = System_class.GetStaticField ("in", "Ljava/io/InputStream;");
				Assert.IsNotNull (System_in);
				Assert.IsTrue (System_in.ID != IntPtr.Zero);
			}
		}

		[TestMethod]
		public unsafe void Name ()
		{
			using (var Object_class         = new JniType ("java/lang/Object"))
			using (var Class_class          = new JniType ("java/lang/Class"))
			using (var Method_class         = new JniType ("java/lang/reflect/Method")) {
				var Class_getMethod         = Class_class.GetInstanceMethod ("getMethod", "(Ljava/lang/String;[Ljava/lang/Class;)Ljava/lang/reflect/Method;");
				var Method_getReturnType    = Method_class.GetInstanceMethod ("getReturnType", "()Ljava/lang/Class;");
				var hashCode_str            = JniEnvironment.Strings.NewString ("hashCode");
				var emptyArray              = JniEnvironment.Arrays.NewObjectArray (0, Class_class.PeerReference, new JniObjectReference ());
				var getHashcodeMethodArgs   = stackalloc JniArgumentValue [2];
				getHashcodeMethodArgs [0]   = new JniArgumentValue (hashCode_str);
				getHashcodeMethodArgs [1]   = new JniArgumentValue (emptyArray);
				var Object_hashCode         = JniEnvironment.InstanceMethods.CallObjectMethod (Object_class.PeerReference, Class_getMethod, getHashcodeMethodArgs);
				var Object_hashCode_rt      = JniEnvironment.InstanceMethods.CallObjectMethod (Object_hashCode, Method_getReturnType);
				try {
					Assert.AreEqual ("java/lang/Object", Object_class.Name);

					using (var t = new JniType (ref Object_hashCode_rt, JniObjectReferenceOptions.Copy))
						Assert.AreEqual ("I", t.Name);
				} finally {
					JniObjectReference.Dispose (ref hashCode_str);
					JniObjectReference.Dispose (ref Object_hashCode);
					JniObjectReference.Dispose (ref Object_hashCode_rt);
					JniObjectReference.Dispose (ref emptyArray);
				}
			}
		}

		[TestMethod]
		public void RegisterWithRuntime ()
		{
			using (var Object_class = new JniType ("java/lang/Object")) {
				Assert.AreEqual (JniObjectReferenceType.Global, Object_class.PeerReference.Type);
				var cur = Object_class.PeerReference;
				Object_class.RegisterWithRuntime ();
				Assert.AreEqual (JniObjectReferenceType.Global, Object_class.PeerReference.Type);
				Assert.IsTrue (Object_class.PeerReference.IsValid);
			}
		}

		[TestMethod]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Test exercises non-AOT-compatible JniType.RegisterNativeMethods API.")]
		public void RegisterNativeMethods ()
		{
			using (var TestType_class = new JniType ("net/dot/jni/test/CallNonvirtualBase")) {
				Assert.AreEqual (JniObjectReferenceType.Global, TestType_class.PeerReference.Type);
				TestType_class.RegisterNativeMethods ();
				Assert.AreEqual (JniObjectReferenceType.Global, TestType_class.PeerReference.Type);
			}
		}

		[TestMethod]
		public unsafe void RegisterNativeMethods_JniNativeMethod ()
		{
			using (var nativeClass = new JniType ("net/dot/jni/test/RegisterNativesTestType")) {
				Span<JniNativeMethod> methods = stackalloc JniNativeMethod [1];
				fixed (byte* namePtr = "add"u8)
				fixed (byte* sigPtr = "(II)I"u8) {
					methods [0] = new JniNativeMethod (namePtr, sigPtr,
						(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, int, int, int>) &NativeAdd);
					JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, methods);
				}

				// Call the native method from Java to verify registration worked
				var ctor = JniEnvironment.InstanceMethods.GetMethodID (nativeClass.PeerReference, "<init>", "()V");
				var obj = JniEnvironment.Object.NewObject (nativeClass.PeerReference, ctor);
				try {
					var addMethod = JniEnvironment.InstanceMethods.GetMethodID (nativeClass.PeerReference, "add", "(II)I");
					var args = stackalloc JniArgumentValue [2];
					args [0] = new JniArgumentValue (3);
					args [1] = new JniArgumentValue (4);
					int result = JniEnvironment.InstanceMethods.CallIntMethod (obj, addMethod, args);
					Assert.AreEqual (7, result);
				} finally {
					JniObjectReference.Dispose (ref obj);
				}
			}
		}

		[UnmanagedCallersOnly]
		static int NativeAdd (IntPtr jnienv, IntPtr self, int a, int b) => a + b;

		[TestMethod]
		public unsafe void RegisterNativeMethods_JniNativeMethod_UnmanagedCallersOnly ()
		{
			using (var nativeClass = new JniType ("net/dot/jni/test/RegisterNativesTestType")) {
				Span<JniNativeMethod> methods = stackalloc JniNativeMethod [1];
				fixed (byte* namePtr = "add"u8)
				fixed (byte* sigPtr = "(II)I"u8) {
					methods [0] = new JniNativeMethod (namePtr, sigPtr,
						(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, int, int, int>) &ManagedAdd);
					JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, methods);
				}

				// Call the native method from Java to verify registration worked
				var ctor = JniEnvironment.InstanceMethods.GetMethodID (nativeClass.PeerReference, "<init>", "()V");
				var obj = JniEnvironment.Object.NewObject (nativeClass.PeerReference, ctor);
				try {
					var addMethod = JniEnvironment.InstanceMethods.GetMethodID (nativeClass.PeerReference, "add", "(II)I");
					var args = stackalloc JniArgumentValue [2];
					args [0] = new JniArgumentValue (5);
					args [1] = new JniArgumentValue (6);
					int result = JniEnvironment.InstanceMethods.CallIntMethod (obj, addMethod, args);
					Assert.AreEqual (11, result);
				} finally {
					JniObjectReference.Dispose (ref obj);
				}
			}
		}

		[UnmanagedCallersOnly]
		static int ManagedAdd (IntPtr jnienv, IntPtr self, int a, int b) => a + b;

		[TestMethod]
		public unsafe void RegisterNativeMethods_JniNativeMethod_UnmanagedCallersOnly_ManyMethods ()
		{
			using (var nativeClass = new JniType ("net/dot/jni/test/RegisterNativesTestType")) {
				// Keep coverage for a large registration set while using the NativeAOT-compatible
				// blittable overload instead of the delegate-marshaling overload.
				var methods = new JniNativeMethod [40];
				fixed (byte* namePtr = "add"u8)
				fixed (byte* sigPtr = "(II)I"u8) {
					for (int i = 0; i < methods.Length; ++i) {
						methods [i] = new JniNativeMethod (namePtr, sigPtr,
							(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, int, int, int>) &ManagedAdd);
					}

					// This should not crash even with > 32 methods.
					JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, methods);
				}

				// Verify the registration worked by calling the method
				var ctor = JniEnvironment.InstanceMethods.GetMethodID (nativeClass.PeerReference, "<init>", "()V");
				var obj = JniEnvironment.Object.NewObject (nativeClass.PeerReference, ctor);
				try {
					var addMethod = JniEnvironment.InstanceMethods.GetMethodID (nativeClass.PeerReference, "add", "(II)I");
					var args = stackalloc JniArgumentValue [2];
					args [0] = new JniArgumentValue (10);
					args [1] = new JniArgumentValue (20);
					int result = JniEnvironment.InstanceMethods.CallIntMethod (obj, addMethod, args);
					Assert.AreEqual (30, result);
				} finally {
					JniObjectReference.Dispose (ref obj);
				}
			}
		}
	}
}
