using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaVMTest
	{
		[Test]
		public void CreateJavaVM ()
		{
			Assert.AreSame (JVM.Current, JavaVM.Current);
			Assert.IsNotNull (JVM.Current.SafeHandle);
			Assert.IsNotNull (JniEnvironment.Current);
		}

		[Test]
		public void JDK_OnlySupportsOneVM ()
		{
			#pragma warning disable 0219
			var first = JVM.Current;
			#pragma warning restore 0219
			try {
				var second = new JreVMBuilder ().CreateJreVM ();
				// If we reach here, we're in a JVM that supports > 1 VM
				second.Dispose ();
				Assert.Ignore ();
			} catch (NotSupportedException) {
			} catch (Exception e){
				Assert.Fail ("Expected NotSupportedException; got: {0}", e);
			}
		}

		[Test, ExpectedException (typeof (ArgumentNullException))]
		public void CreateJavaVMWithNullBuilder ()
		{
			new JavaVMWithNullBuilder ();
		}

		class JavaVMWithNullBuilder : JavaVM {
			public JavaVMWithNullBuilder ()
				: base ((JavaVMOptions) null)
			{
			}

			protected override bool TryGC (IJavaObject value, ref JniReferenceSafeHandle handle)
			{
				throw new NotImplementedException ();
			}
		}

		[Test]
		public void GetRegisteredJavaVM_ExistingInstance ()
		{
			Assert.AreEqual (JavaVM.Current, JavaVM.GetRegisteredJavaVM (JavaVM.Current.SafeHandle));
		}

		[Test]
		public void GetObject_ReturnsNullWithNullHandle ()
		{
			var o = JVM.Current.GetObject (IntPtr.Zero);
			Assert.IsNull (o);
		}

		[Test]
		public void GetObject_ReturnsRegisteredInstance ()
		{
			JniLocalReference lref;
			using (var o = new JavaObject ()) {
				lref = o.SafeHandle.NewLocalRef ();
				Assert.IsNull (JVM.Current.GetObject (lref.DangerousGetHandle ()));
				o.RegisterWithVM ();
				Assert.AreSame (o, JVM.Current.GetObject (lref.DangerousGetHandle ()));
			}
			// At this point, the Java-side object is kept alive by `lref`,
			// but the wrapper instance has been disposed, and thus should
			// be unregistered, and thus unfindable.
			Assert.IsNull (JVM.Current.GetObject (lref, JniHandleOwnership.Transfer));
			Assert.IsTrue (lref.IsInvalid);
		}

		[Test]
		public void GetObject_ReturnsNullWithInvalidSafeHandle ()
		{
			var invalid = JniReferenceSafeHandle.Null;
			Assert.IsNull (JVM.Current.GetObject (invalid, JniHandleOwnership.Transfer));
		}

		[Test]
		public void GetJniTypeInfoForType ()
		{
			Assert.Throws<ArgumentNullException> (() => JVM.Current.GetJniTypeInfoForType (null));
			Assert.Throws<ArgumentException>(() => JVM.Current.GetJniTypeInfoForType (typeof (int[,])));
			Assert.Throws<ArgumentException>(() => JVM.Current.GetJniTypeInfoForType (typeof (int[,][])));
			Assert.Throws<ArgumentException>(() => JVM.Current.GetJniTypeInfoForType (typeof (int[][,])));
			Assert.Throws<ArgumentException>(() => JVM.Current.GetJniTypeInfoForType (typeof (Action<>)));

			// TODO? Not currently mapped to java/lang/String; should it?
			AssertGetJniTypeInfoForType (typeof (string),   null,   false,  0);

			AssertGetJniTypeInfoForType (typeof (sbyte),    "B",    true,   0);
			AssertGetJniTypeInfoForType (typeof (short),    "S",    true,   0);
			AssertGetJniTypeInfoForType (typeof (int),      "I",    true,   0);
			AssertGetJniTypeInfoForType (typeof (long),     "J",    true,   0);
			AssertGetJniTypeInfoForType (typeof (float),    "F",    true,   0);
			AssertGetJniTypeInfoForType (typeof (double),   "D",    true,   0);
			AssertGetJniTypeInfoForType (typeof (char),     "C",    true,   0);
			AssertGetJniTypeInfoForType (typeof (bool),     "Z",    true,   0);
			AssertGetJniTypeInfoForType (typeof (void),     "V",    true,   0);

			AssertGetJniTypeInfoForType (typeof (JavaObject),  "java/lang/Object",  false,  0);

			// Enums are their underlying type
			AssertGetJniTypeInfoForType (typeof (StringComparison),     "I",    true,   0);
			AssertGetJniTypeInfoForType (typeof (StringComparison[]),   "[I",   true,   1);
			AssertGetJniTypeInfoForType (typeof (StringComparison[][]), "[[I",  true,   2);

			AssertGetJniTypeInfoForType (typeof (int[]),        "[I",   true,   1);
			AssertGetJniTypeInfoForType (typeof (int[][]),      "[[I",  true,   2);
			AssertGetJniTypeInfoForType (typeof (int[][][]),    "[[[I", true,   3);

			AssertGetJniTypeInfoForType (typeof (JavaSByteArray),       "[B",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaInt16Array),       "[S",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaInt32Array),       "[I",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaInt64Array),       "[J",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaSingleArray),      "[F",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaDoubleArray),      "[D",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaCharArray),        "[C",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaBooleanArray),     "[Z",   true,   1);

			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<SByte>),    "[B",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<Int16>),    "[S",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<Int32>),    "[I",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<Int64>),    "[J",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<Single>),   "[F",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<Double>),   "[D",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<Char>),     "[C",   true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<Boolean>),  "[Z",   true,   1);

			AssertGetJniTypeInfoForType (typeof (JavaArray<SByte>),    "[B",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Int16>),    "[S",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Int32>),    "[I",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Int64>),    "[J",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Single>),   "[F",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Double>),   "[D",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Char>),     "[C",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Boolean>),  "[Z",    true,   1);

			AssertGetJniTypeInfoForType (typeof (JavaArray<Boolean>[]), "[[Z",  true,   2);

			AssertGetJniTypeInfoForType (typeof (JavaArray<JavaObject>),    "[Ljava/lang/Object;",  false,  1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<int[]>),         "[[I",                  true,   2);
			AssertGetJniTypeInfoForType (typeof (JavaArray<int[]>[]),       "[[[I",                 true,   3);
		}

		static void AssertGetJniTypeInfoForType (Type type, string jniType, bool isKeyword, int arrayRank)
		{
			var info = JVM.Current.GetJniTypeInfoForType (type);
			Assert.AreEqual (jniType,   info.ToString ());
			Assert.AreEqual (isKeyword, info.TypeIsKeyword);
			Assert.AreEqual (arrayRank, info.ArrayRank);
		}
	}
}

