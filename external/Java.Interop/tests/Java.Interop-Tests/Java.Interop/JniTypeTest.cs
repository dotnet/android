using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeTest : JavaVMFixture {

		[Test]
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

		[Test]
		public void Ctor_ThrowsIfTypeNotFound ()
		{
#if __ANDROID__
			Assert.Throws<Java.Lang.ClassNotFoundException> (() => new JniType ("__this__/__type__/__had__/__better__/__not__/__Exist__")).Dispose ();
#else   // __ANDROID__
			Assert.Throws<JavaException> (() => new JniType ("__this__/__type__/__had__/__better__/__not__/__Exist__")).Dispose ();
#endif  // __ANDROID__
		}

		[Test]
		public unsafe void Dispose_Exceptions ()
		{
			var t = new JniType ("java/lang/Object");
			t.Dispose ();
			Assert.Throws<ObjectDisposedException> (() => t.AllocObject ());
			Assert.Throws<ObjectDisposedException> (() => t.NewObject (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetConstructor (null));
			Assert.Throws<ObjectDisposedException> (() => t.GetInstanceField (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetInstanceMethod (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetStaticField (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetStaticMethod (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetSuperclass ());
			Assert.Throws<ObjectDisposedException> (() => t.IsAssignableFrom (null));
			Assert.Throws<ObjectDisposedException> (() => t.IsInstanceOfType (new JniObjectReference ()));
			Assert.Throws<ObjectDisposedException> (() => t.RegisterWithRuntime ());
			Assert.Throws<ObjectDisposedException> (() => t.RegisterNativeMethods (null));
			Assert.Throws<ObjectDisposedException> (() => t.UnregisterNativeMethods ());

			JniFieldInfo    jif = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedInstanceField (ref jif, null, null));
			JniMethodInfo   jim = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedConstructor (ref jim, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedInstanceMethod (ref jim, null, null));
			JniFieldInfo    jsf = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedStaticField (ref jsf, null, null));
			JniMethodInfo   jsm = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedStaticMethod (ref jsm, null, null));
		}

		[Test]
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

		[Test]
		public void IsAssignableFrom ()
		{
			using (var o = new JniType ("java/lang/Object"))
			using (var s = new JniType ("java/lang/String")) {
				Assert.IsTrue (o.IsAssignableFrom (s));
				Assert.IsFalse (s.IsAssignableFrom (o));
			}
		}

		[Test]
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

		[Test]
		public void InvalidSignatureThrowsJniException ()
		{
			using (var Integer_class = new JniType ("java/lang/Integer")) {
#if __ANDROID__
				Assert.Throws<Java.Lang.NoSuchMethodError> (() => Integer_class.GetConstructor ("(C)V")).Dispose ();
#else   // __ANDROID__
				Assert.Throws<JavaException> (() => Integer_class.GetConstructor ("(C)V")).Dispose ();
#endif  // __ANDROID__
			}
		}

		[Test]
		public void GetStaticFieldID ()
		{
			using (var System_class = new JniType ("java/lang/System")) {
				var System_in = System_class.GetStaticField ("in", "Ljava/io/InputStream;");
				Assert.IsNotNull (System_in);
				Assert.IsTrue (System_in.ID != IntPtr.Zero);
			}
		}

		[Test]
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

		[Test]
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

		[Test]
		public void RegisterNativeMethods ()
		{
			using (var TestType_class = new JniType ("com/xamarin/interop/CallNonvirtualBase")) {
				Assert.AreEqual (JniObjectReferenceType.Global, TestType_class.PeerReference.Type);
				TestType_class.RegisterNativeMethods ();
				Assert.AreEqual (JniObjectReferenceType.Global, TestType_class.PeerReference.Type);
			}
		}
	}
}

