#nullable enable

using System;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JniTypeUtf8Test {

		const string JniReferenceLeakCategory = "JniReferenceLeak";
		public TestContext? TestContext { get; set; }

		[TestMethod]
		public void Sanity_Utf8 ()
		{
			// SAFETY: UTF-8 literals have NUL-terminated backing storage, and the type remains live.
			using (var Integer_class = unsafe (new JniType ("java/lang/Integer"u8))) {
				Assert.AreEqual ("java/lang/Integer", Integer_class.Name);

				// SAFETY: These terminated literals describe members of the live Integer type.
				var Integer_ctor     = unsafe (Integer_class.GetConstructor ("(I)V"u8));
				var Integer_intValue = unsafe (Integer_class.GetInstanceMethod ("intValue"u8, "()I"u8));
				JniObjectReference o;
				// SAFETY: The one-element stack buffer matches (I)V and is consumed synchronously.
				unsafe {
					var ctor_args = stackalloc JniArgumentValue [1];
					ctor_args [0] = new JniArgumentValue (42);
					o = Integer_class.NewObject (Integer_ctor, ctor_args);
				}
				try {
					// SAFETY: The live Integer peer's ()I method takes no arguments.
					int v = unsafe (JniEnvironment.InstanceMethods.CallIntMethod (o, Integer_intValue));
					Assert.AreEqual (42, v);
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}

		[TestMethod]
		public void FindClass_Utf8_ReturnsValidReference ()
		{
			// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
			var r = unsafe (JniEnvironment.Types.FindClass ("java/lang/Object"u8));
			try {
				Assert.IsTrue (r.IsValid);
			} finally {
				JniObjectReference.Dispose (ref r);
			}
		}

		[TestMethod]
		public void FindClass_Utf8_UsesSameFallbackAsStringOverload ()
		{
			var fromString = JniEnvironment.Types.FindClass ("java.lang.Object");
			// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
			var fromUtf8   = unsafe (JniEnvironment.Types.FindClass ("java.lang.Object"u8));
			try {
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString, fromUtf8));
			} finally {
				JniObjectReference.Dispose (ref fromString);
				JniObjectReference.Dispose (ref fromUtf8);
			}
		}

		[TestMethod]
		public void Ctor_Utf8_UsesSameFallbackAsStringOverload ()
		{
			using (var fromString = new JniType ("java.lang.Object"))
			// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
			using (var fromUtf8 = unsafe (new JniType ("java.lang.Object"u8))) {
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString.PeerReference, fromUtf8.PeerReference));
				Assert.AreEqual ("java/lang/Object", fromUtf8.Name);
			}
		}

		[TestMethod]
		public void FindClass_Utf8_ThrowsOnNotFound ()
		{
			// SAFETY: The UTF-8 literal is terminated; only the class lookup is expected to fail.
			Assert.ThrowsExactly<Java.Lang.ClassNotFoundException> (() => unsafe (JniEnvironment.Types.FindClass ("does/not/Exist"u8)));
		}

		[TestMethod]
		public void TryFindClass_Utf8 ()
		{
			// SAFETY: Both UTF-8 literals have NUL-terminated backing storage.
			Assert.IsTrue (unsafe (JniEnvironment.Types.TryFindClass ("java/lang/Object"u8, out var found)));
			try {
				Assert.IsTrue (found.IsValid);
			} finally {
				JniObjectReference.Dispose (ref found);
			}

			Assert.IsFalse (unsafe (JniEnvironment.Types.TryFindClass ("does/not/Exist"u8, out var notFound)));
			Assert.IsFalse (notFound.IsValid);
		}

		[TestMethod]
		public void TryFindClass_Utf8_UsesSpanLength ()
		{
			// SAFETY: Each span remains readable and unchanged during lookup; TryFindClass bounds reads to the span length or an earlier NUL.
			Assert.IsTrue (unsafe (JniEnvironment.Types.TryFindClass ("java/lang/ObjectDoesNotExist"u8.Slice (0, 16), out var found)));
			try {
				Assert.IsTrue (found.IsValid);
			} finally {
				JniObjectReference.Dispose (ref found);
			}

			Assert.IsTrue (unsafe (JniEnvironment.Types.TryFindClass ("java.lang.ObjectDoesNotExist"u8.Slice (0, 16), out var foundDotted)));
			try {
				Assert.IsTrue (foundDotted.IsValid);
			} finally {
				JniObjectReference.Dispose (ref foundDotted);
			}

			Assert.IsFalse (unsafe (JniEnvironment.Types.TryFindClass ("java/lang/Object"u8.Slice (0, 13), out var truncated)));
			Assert.IsFalse (truncated.IsValid);

			Assert.ThrowsExactly<ArgumentException> (() => unsafe (JniEnvironment.Types.TryFindClass ("\0java/lang/Object"u8, out _)));
		}

		[TestMethod]
		[TestCategory (JniReferenceLeakCategory)]
		public void TryFindClass_Utf8_DoesNotLeakGlobalRefs ()
		{
			AssertNoSustainedGlobalReferenceGrowth (() => {
				// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
				Assert.IsFalse (unsafe (JniEnvironment.Types.TryFindClass ("does/not/Exist"u8, out var notFound)));
				Assert.IsFalse (notFound.IsValid);
			});
		}

		[TestMethod]
		[TestCategory (JniReferenceLeakCategory)]
		public void TryFindClass_String_DoesNotLeakGlobalRefs ()
		{
			AssertNoSustainedGlobalReferenceGrowth (() => {
				Assert.IsFalse (JniEnvironment.Types.TryFindClass ("does/not/Exist", out var notFound));
				Assert.IsFalse (notFound.IsValid);
			});
		}

		void AssertNoSustainedGlobalReferenceGrowth (Action action)
		{
			var context = TestContext ?? throw new InvalidOperationException ("The runner did not initialize TestContext.");
			JniReferenceLeakMeasurement.AssertNoSustainedGlobalReferenceGrowth (action, context.WriteLine);
		}

		[TestMethod]
		public void GetMethodID_Utf8_MatchesStringOverload ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var Object_class = unsafe (new JniType ("java/lang/Object"u8))) {
				var fromString = unsafe (JniEnvironment.InstanceMethods.GetMethodID (Object_class.PeerReference, "hashCode", "()I"));
				var fromUtf8   = unsafe (JniEnvironment.InstanceMethods.GetMethodID (Object_class.PeerReference, "hashCode"u8, "()I"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetStaticMethodID_Utf8_MatchesStringOverload ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var fromString = unsafe (JniEnvironment.StaticMethods.GetStaticMethodID (System_class.PeerReference, "currentTimeMillis", "()J"));
				var fromUtf8   = unsafe (JniEnvironment.StaticMethods.GetStaticMethodID (System_class.PeerReference, "currentTimeMillis"u8, "()J"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetFieldID_Utf8_MatchesStringOverload ()
		{
			// Integer.value is private and blocked by ART hidden API restrictions.
			// StreamTokenizer.ttype is a public instance field available on both JVM and Android.
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var StreamTokenizer_class = unsafe (new JniType ("java/io/StreamTokenizer"u8))) {
				var fromString = unsafe (JniEnvironment.InstanceFields.GetFieldID (StreamTokenizer_class.PeerReference, "ttype", "I"));
				var fromUtf8   = unsafe (JniEnvironment.InstanceFields.GetFieldID (StreamTokenizer_class.PeerReference, "ttype"u8, "I"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetStaticFieldID_Utf8_MatchesStringOverload ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var fromString = unsafe (JniEnvironment.StaticFields.GetStaticFieldID (System_class.PeerReference, "in", "Ljava/io/InputStream;"));
				var fromUtf8   = unsafe (JniEnvironment.StaticFields.GetStaticFieldID (System_class.PeerReference, "in"u8, "Ljava/io/InputStream;"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetStaticField_Utf8 ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live during field lookup.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var System_in = unsafe (System_class.GetStaticField ("in"u8, "Ljava/io/InputStream;"u8));
				Assert.IsNotNull (System_in);
				Assert.IsTrue (System_in.ID != IntPtr.Zero);
			}
		}

		[TestMethod]
		public void GetStaticMethod_Utf8_Invocation ()
		{
			// SAFETY: Literals are terminated, and the live type owns this no-argument static method.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var currentTimeMillis = unsafe (System_class.GetStaticMethod ("currentTimeMillis"u8, "()J"u8));
				long time = unsafe (JniEnvironment.StaticMethods.CallStaticLongMethod (System_class.PeerReference, currentTimeMillis));
				Assert.IsTrue (time > 0);
			}
		}

		[TestMethod]
		public void GetCachedInstanceMethod_Utf8_ReturnsSameInstance ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout the cached lookups.
			using (var Object_class = unsafe (new JniType ("java/lang/Object"u8))) {
				JniMethodInfo? cached = null;
				var first  = unsafe (Object_class.GetCachedInstanceMethod (ref cached, "hashCode"u8, "()I"u8));
				var second = unsafe (Object_class.GetCachedInstanceMethod (ref cached, "hashCode"u8, "()I"u8));
				Assert.AreSame (first, second);
			}
		}

		[TestMethod]
		public void GetCachedStaticMethod_Utf8_ReturnsSameInstance ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout the cached lookups.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				JniMethodInfo? cached = null;
				var first  = unsafe (System_class.GetCachedStaticMethod (ref cached, "currentTimeMillis"u8, "()J"u8));
				var second = unsafe (System_class.GetCachedStaticMethod (ref cached, "currentTimeMillis"u8, "()J"u8));
				Assert.AreSame (first, second);
			}
		}

		[TestMethod]
		public void Dispose_Exceptions_Utf8 ()
		{
			// SAFETY: All UTF-8 inputs below are terminated literals; disposed-type checks precede JNI dispatch.
			var t = unsafe (new JniType ("java/lang/Object"u8));
			t.Dispose ();

			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetConstructor ("()V"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetInstanceField ("value"u8, "I"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetInstanceMethod ("hashCode"u8, "()I"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetStaticField ("in"u8, "Ljava/io/InputStream;"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetStaticMethod ("currentTimeMillis"u8, "()J"u8)));

			JniFieldInfo?  jif = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedInstanceField (ref jif, "value"u8, "I"u8)));
			JniMethodInfo? jim = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedConstructor (ref jim, "()V"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedInstanceMethod (ref jim, "hashCode"u8, "()I"u8)));
			JniFieldInfo?  jsf = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedStaticField (ref jsf, "in"u8, "Ljava/io/InputStream;"u8)));
			JniMethodInfo? jsm = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedStaticMethod (ref jsm, "currentTimeMillis"u8, "()J"u8)));
		}

		[TestMethod]
		public void InvalidSignature_Utf8_ThrowsJniException ()
		{
			// SAFETY: Literals are terminated and the live type owns the lookup; the absent method must fail.
			using (var Object_class = unsafe (new JniType ("java/lang/Object"u8))) {
				Assert.ThrowsExactly<Java.Lang.NoSuchMethodError> (() => unsafe (Object_class.GetInstanceMethod ("bogus"u8, "(Z)V"u8)));
			}
		}
	}
}
