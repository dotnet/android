using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeTest {

		[Test]
		public void Sanity ()
		{
			using (var Integer_class = new JniType ("java/lang/Integer")) {
				var Integer_ctor        = Integer_class.GetConstructor ("(I)V");
				var Integer_intValue    = Integer_class.GetInstanceMethod ("intValue", "()I");
				using (var o = Integer_class.NewObject (Integer_ctor, new JValue (42))) {
					int v = Integer_intValue.CallVirtualInt32Method (o);
					Assert.AreEqual (42, v);
				}
			}
		}

		[Test]
		public void Ctor_ThrowsIfTypeNotFound ()
		{
			Assert.Throws<JavaException> (() => new JniType ("__this__/__type__/__had__/__better__/__not__/__Exist__")).Dispose ();
		}

		[Test]
		public void Dispose_Exceptions ()
		{
			var t = new JniType ("java/lang/Object");
			t.Dispose ();
			Assert.Throws<ObjectDisposedException> (() => t.AllocObject ());
			Assert.Throws<ObjectDisposedException> (() => t.NewObject (null));
			Assert.Throws<ObjectDisposedException> (() => t.GetConstructor (null));
			Assert.Throws<ObjectDisposedException> (() => t.GetInstanceField (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetInstanceMethod (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetStaticField (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetStaticMethod (null, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetSuperclass ());
			Assert.Throws<ObjectDisposedException> (() => t.IsAssignableFrom (null));
			Assert.Throws<ObjectDisposedException> (() => t.IsInstanceOfType (null));
			Assert.Throws<ObjectDisposedException> (() => t.RegisterWithVM ());
			Assert.Throws<ObjectDisposedException> (() => t.RegisterNativeMethods (null));
			Assert.Throws<ObjectDisposedException> (() => t.UnregisterNativeMethods ());

			JniInstanceFieldID jif = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedInstanceField (ref jif, null, null));
			JniInstanceMethodID jim = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedConstructor (ref jim, null));
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedInstanceMethod (ref jim, null, null));
			JniStaticFieldID jsf = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedStaticField (ref jsf, null, null));
			JniStaticMethodID jsm = null;
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
						Assert.IsTrue (JniEnvironment.Types.IsSameObject (t.SafeHandle, st.SafeHandle));
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
		public void IsInstanceOfType ()
		{
			using (var t = new JniType ("java/lang/Object"))
			using (var b = new TestType ()) {
				Assert.IsTrue (t.IsInstanceOfType (b.SafeHandle));
			}
		}

		[Test]
		public void ObjectBinding ()
		{
			using (var b = new TestType ()) {
				Console.WriteLine ("# ObjectBinding: {0}", b.ToString ());
			}
		}

		[Test]
		public void InvalidSignatureThrowsJniException ()
		{
			using (var Integer_class = new JniType ("java/lang/Integer")) {
				Assert.Throws<JavaException> (() => Integer_class.GetConstructor ("(C)V")).Dispose ();
			}
		}

		[Test]
		public void GetStaticFieldID ()
		{
			using (var System_class = new JniType ("java/lang/System")) {
				var System_in = System_class.GetStaticField ("in", "Ljava/io/InputStream;");
				Assert.IsNotNull (System_in);
			}
		}

		[Test]
		public void RegisterWithVM ()
		{
			using (var Object_class = new JniType ("java/lang/Object")) {
				Assert.AreEqual (JniReferenceType.Local, Object_class.SafeHandle.ReferenceType);
				var cur = Object_class.SafeHandle;
				Object_class.RegisterWithVM ();
				Assert.AreEqual (JniReferenceType.Global, Object_class.SafeHandle.ReferenceType);
				Assert.IsTrue (cur.IsInvalid);
			}
		}

		[Test]
		public void RegisterNativeMethods ()
		{
			using (var TestType_class = new JniType ("com/xamarin/interop/CallNonvirtualBase")) {
				Assert.AreEqual (JniReferenceType.Local, TestType_class.SafeHandle.ReferenceType);
				TestType_class.RegisterNativeMethods ();
				Assert.AreEqual (JniReferenceType.Global, TestType_class.SafeHandle.ReferenceType);
			}
		}
	}
}

