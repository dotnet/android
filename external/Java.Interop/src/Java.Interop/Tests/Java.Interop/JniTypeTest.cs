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
			Assert.Throws<JniException> (() => new JniType ("__this__/__type__/__had__/__better__/__not__/__Exist__"));
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
			using (var b = new TestObjectBinding ()) {
				Assert.IsTrue (t.IsInstanceOfType (b.SafeHandle));
			}
		}

		[Test]
		public void ObjectBinding ()
		{
			using (var b = new TestObjectBinding ()) {
				Console.WriteLine ("# ObjectBinding: {0}", b.ToString ());
			}
		}

		[Test, ExpectedException (typeof (JniException))]
		public void InvalidSignatureThrowsJniException ()
		{
			using (var Integer_class = new JniType ("java/lang/Integer")) {
				Integer_class.GetConstructor ("(C)V");
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
	}
}

