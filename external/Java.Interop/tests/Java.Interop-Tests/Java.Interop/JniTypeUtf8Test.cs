#nullable enable

using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeUtf8Test : JavaVMFixture {

		[Test]
		public unsafe void Sanity_Utf8 ()
		{
			using (var Integer_class = new JniType ("java/lang/Integer"u8)) {
				Assert.AreEqual ("java/lang/Integer", Integer_class.Name);

				var ctor_args = stackalloc JniArgumentValue [1];
				ctor_args [0] = new JniArgumentValue (42);

				var Integer_ctor     = Integer_class.GetConstructor ("(I)V"u8);
				var Integer_intValue = Integer_class.GetInstanceMethod ("intValue"u8, "()I"u8);
				var o                = Integer_class.NewObject (Integer_ctor, ctor_args);
				try {
					int v = JniEnvironment.InstanceMethods.CallIntMethod (o, Integer_intValue);
					Assert.AreEqual (42, v);
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}

		[Test]
		public void FindClass_Utf8_ReturnsValidReference ()
		{
			var r = JniEnvironment.Types.FindClass ("java/lang/Object"u8);
			try {
				Assert.IsTrue (r.IsValid);
			} finally {
				JniObjectReference.Dispose (ref r);
			}
		}

		[Test]
		public void FindClass_Utf8_UsesSameFallbackAsStringOverload ()
		{
			var fromString = JniEnvironment.Types.FindClass ("java.lang.Object");
			var fromUtf8   = JniEnvironment.Types.FindClass ("java.lang.Object"u8);
			try {
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString, fromUtf8));
			} finally {
				JniObjectReference.Dispose (ref fromString);
				JniObjectReference.Dispose (ref fromUtf8);
			}
		}

		[Test]
		public void Ctor_Utf8_UsesSameFallbackAsStringOverload ()
		{
			using (var fromString = new JniType ("java.lang.Object"))
			using (var fromUtf8 = new JniType ("java.lang.Object"u8)) {
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString.PeerReference, fromUtf8.PeerReference));
				Assert.AreEqual ("java/lang/Object", fromUtf8.Name);
			}
		}

		[Test]
		public void FindClass_Utf8_ThrowsOnNotFound ()
		{
#if __ANDROID__
			Assert.Throws<Java.Lang.ClassNotFoundException> (() => JniEnvironment.Types.FindClass ("does/not/Exist"u8));
#else   // __ANDROID__
			Assert.Throws<JavaException> (() => JniEnvironment.Types.FindClass ("does/not/Exist"u8));
#endif  // __ANDROID__
		}

		[Test]
		public void TryFindClass_Utf8 ()
		{
			Assert.IsTrue (JniEnvironment.Types.TryFindClass ("java/lang/Object"u8, out var found));
			try {
				Assert.IsTrue (found.IsValid);
			} finally {
				JniObjectReference.Dispose (ref found);
			}

			Assert.IsFalse (JniEnvironment.Types.TryFindClass ("does/not/Exist"u8, out var notFound));
			Assert.IsFalse (notFound.IsValid);
		}

		[Test]
		public void GetMethodID_Utf8_MatchesStringOverload ()
		{
			using (var Object_class = new JniType ("java/lang/Object"u8)) {
				var fromString = JniEnvironment.InstanceMethods.GetMethodID (Object_class.PeerReference, "hashCode", "()I");
				var fromUtf8   = JniEnvironment.InstanceMethods.GetMethodID (Object_class.PeerReference, "hashCode"u8, "()I"u8);
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[Test]
		public void GetStaticMethodID_Utf8_MatchesStringOverload ()
		{
			using (var System_class = new JniType ("java/lang/System"u8)) {
				var fromString = JniEnvironment.StaticMethods.GetStaticMethodID (System_class.PeerReference, "currentTimeMillis", "()J");
				var fromUtf8   = JniEnvironment.StaticMethods.GetStaticMethodID (System_class.PeerReference, "currentTimeMillis"u8, "()J"u8);
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[Test]
		public void GetFieldID_Utf8_MatchesStringOverload ()
		{
			// Integer.value is private and blocked by ART hidden API restrictions.
			// StreamTokenizer.ttype is a public instance field available on both JVM and Android.
			using (var StreamTokenizer_class = new JniType ("java/io/StreamTokenizer"u8)) {
				var fromString = JniEnvironment.InstanceFields.GetFieldID (StreamTokenizer_class.PeerReference, "ttype", "I");
				var fromUtf8   = JniEnvironment.InstanceFields.GetFieldID (StreamTokenizer_class.PeerReference, "ttype"u8, "I"u8);
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[Test]
		public void GetStaticFieldID_Utf8_MatchesStringOverload ()
		{
			using (var System_class = new JniType ("java/lang/System"u8)) {
				var fromString = JniEnvironment.StaticFields.GetStaticFieldID (System_class.PeerReference, "in", "Ljava/io/InputStream;");
				var fromUtf8   = JniEnvironment.StaticFields.GetStaticFieldID (System_class.PeerReference, "in"u8, "Ljava/io/InputStream;"u8);
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[Test]
		public void GetStaticField_Utf8 ()
		{
			using (var System_class = new JniType ("java/lang/System"u8)) {
				var System_in = System_class.GetStaticField ("in"u8, "Ljava/io/InputStream;"u8);
				Assert.IsNotNull (System_in);
				Assert.IsTrue (System_in.ID != IntPtr.Zero);
			}
		}

		[Test]
		public void GetStaticMethod_Utf8_Invocation ()
		{
			using (var System_class = new JniType ("java/lang/System"u8)) {
				var currentTimeMillis = System_class.GetStaticMethod ("currentTimeMillis"u8, "()J"u8);
				long time = JniEnvironment.StaticMethods.CallStaticLongMethod (System_class.PeerReference, currentTimeMillis);
				Assert.IsTrue (time > 0);
			}
		}

		[Test]
		public void GetCachedInstanceMethod_Utf8_ReturnsSameInstance ()
		{
			using (var Object_class = new JniType ("java/lang/Object"u8)) {
				JniMethodInfo? cached = null;
				var first  = Object_class.GetCachedInstanceMethod (ref cached, "hashCode"u8, "()I"u8);
				var second = Object_class.GetCachedInstanceMethod (ref cached, "hashCode"u8, "()I"u8);
				Assert.AreSame (first, second);
			}
		}

		[Test]
		public void GetCachedStaticMethod_Utf8_ReturnsSameInstance ()
		{
			using (var System_class = new JniType ("java/lang/System"u8)) {
				JniMethodInfo? cached = null;
				var first  = System_class.GetCachedStaticMethod (ref cached, "currentTimeMillis"u8, "()J"u8);
				var second = System_class.GetCachedStaticMethod (ref cached, "currentTimeMillis"u8, "()J"u8);
				Assert.AreSame (first, second);
			}
		}

		[Test]
		public void Dispose_Exceptions_Utf8 ()
		{
			var t = new JniType ("java/lang/Object"u8);
			t.Dispose ();

			Assert.Throws<ObjectDisposedException> (() => t.GetConstructor ("()V"u8));
			Assert.Throws<ObjectDisposedException> (() => t.GetInstanceField ("value"u8, "I"u8));
			Assert.Throws<ObjectDisposedException> (() => t.GetInstanceMethod ("hashCode"u8, "()I"u8));
			Assert.Throws<ObjectDisposedException> (() => t.GetStaticField ("in"u8, "Ljava/io/InputStream;"u8));
			Assert.Throws<ObjectDisposedException> (() => t.GetStaticMethod ("currentTimeMillis"u8, "()J"u8));

			JniFieldInfo?  jif = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedInstanceField (ref jif, "value"u8, "I"u8));
			JniMethodInfo? jim = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedConstructor (ref jim, "()V"u8));
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedInstanceMethod (ref jim, "hashCode"u8, "()I"u8));
			JniFieldInfo?  jsf = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedStaticField (ref jsf, "in"u8, "Ljava/io/InputStream;"u8));
			JniMethodInfo? jsm = null;
			Assert.Throws<ObjectDisposedException> (() => t.GetCachedStaticMethod (ref jsm, "currentTimeMillis"u8, "()J"u8));
		}

		[Test]
		public void InvalidSignature_Utf8_ThrowsJniException ()
		{
			using (var Object_class = new JniType ("java/lang/Object"u8)) {
#if __ANDROID__
				Assert.Throws<Java.Lang.NoSuchMethodError> (() => Object_class.GetInstanceMethod ("bogus"u8, "(Z)V"u8));
#else   // __ANDROID__
				Assert.Throws<JavaException> (() => Object_class.GetInstanceMethod ("bogus"u8, "(Z)V"u8));
#endif  // __ANDROID__
			}
		}
	}
}
