using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeManagerTests : JavaVMFixture
	{
		[Test]
		public void GetTypeSignature_Type ()
		{
			Assert.Throws<ArgumentNullException> (() => JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature ((Type) null));
			Assert.Throws<ArgumentException>(() => JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (typeof (int[,])));
			Assert.Throws<ArgumentException>(() => JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (typeof (int[,][])));
			Assert.Throws<ArgumentException>(() => JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (typeof (int[][,])));
			Assert.Throws<ArgumentException>(() => JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (typeof (Action<>)));
			Assert.AreEqual (null, JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (typeof (JniRuntimeTest)).SimpleReference);

			AssertGetJniTypeInfoForType (typeof (string),   "java/lang/String",   false,  0);

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
			AssertGetJniTypeInfoForType (typeof (JavaPrimitiveArray<bool>),     "[Z",   true,   1);

			AssertGetJniTypeInfoForType (typeof (JavaArray<SByte>),    "[B",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Int16>),    "[S",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Int32>),    "[I",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Int64>),    "[J",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Single>),   "[F",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Double>),   "[D",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<Char>),     "[C",    true,   1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<bool>),     "[Z",    true,   1);

			AssertGetJniTypeInfoForType (typeof (JavaArray<bool>[]),   "[[Z",  true,   2);

			AssertGetJniTypeInfoForType (typeof (JavaArray<JavaObject>),    "[Ljava/lang/Object;",  false,  1);
			AssertGetJniTypeInfoForType (typeof (JavaArray<int[]>),         "[[I",                  true,   2);
			AssertGetJniTypeInfoForType (typeof (JavaArray<int[]>[]),       "[[[I",                 true,   3);

			AssertGetJniTypeInfoForType (typeof (GenericHolder<int>),       GenericHolder<int>.JniTypeName,    false,   0);
		}

		static void AssertGetJniTypeInfoForType (Type type, string jniType, bool isKeyword, int arrayRank)
		{
			var info = JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (type);
			Assert.AreEqual (jniType,   info.Name);
			Assert.AreEqual (arrayRank, info.ArrayRank);
		}

		[Test]
		public new void GetType ()
		{
			var manager = JniRuntime.CurrentRuntime.TypeManager;
			Func<string, Type> GetType = s => {
				var sig = JniTypeSignature.Parse (s);
				return manager.GetType (sig);
			};
			Assert.Throws<ArgumentNullException> (() => GetType (null));
			Assert.Throws<ArgumentException> (() => GetType ("java.lang.String"));
			Assert.Throws<ArgumentException> (() => GetType ("Ljava/lang/String;I"));
			Assert.Throws<ArgumentException> (() => GetType ("ILjava/lang/String;"));

			Assert.AreEqual (typeof (void),     GetType ("V"));
			Assert.AreEqual (typeof (bool),     GetType ("Z"));
			Assert.AreEqual (typeof (char),     GetType ("C"));
			Assert.AreEqual (typeof (sbyte),    GetType ("B"));
			Assert.AreEqual (typeof (short),    GetType ("S"));
			Assert.AreEqual (typeof (int),      GetType ("I"));
			Assert.AreEqual (typeof (long),     GetType ("J"));
			Assert.AreEqual (typeof (float),    GetType ("F"));
			Assert.AreEqual (typeof (double),   GetType ("D"));
			Assert.AreEqual (typeof (string),   GetType ("java/lang/String"));
			Assert.AreEqual (null,              GetType ("com/example/does/not/exist"));
			Assert.AreEqual (null,              GetType ("Lcom/example/does/not/exist;"));
			Assert.AreEqual (null,              GetType ("[Lcom/example/does/not/exist;"));

			Assert.AreEqual (typeof (JavaPrimitiveArray<bool>),     GetType ("[Z"));
			Assert.AreEqual (typeof (JavaPrimitiveArray<char>),     GetType ("[C"));
			Assert.AreEqual (typeof (JavaPrimitiveArray<sbyte>),    GetType ("[B"));
			Assert.AreEqual (typeof (JavaPrimitiveArray<short>),    GetType ("[S"));
			Assert.AreEqual (typeof (JavaPrimitiveArray<int>),      GetType ("[I"));
			Assert.AreEqual (typeof (JavaPrimitiveArray<long>),     GetType ("[J"));
			Assert.AreEqual (typeof (JavaPrimitiveArray<float>),    GetType ("[F"));
			Assert.AreEqual (typeof (JavaPrimitiveArray<double>),   GetType ("[D"));
			Assert.AreEqual (typeof (JavaObjectArray<string>),      GetType ("[Ljava/lang/String;"));

			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<bool>>),    GetType ("[[Z"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<char>>),    GetType ("[[C"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<sbyte>>),   GetType ("[[B"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<short>>),   GetType ("[[S"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<int>>),     GetType ("[[I"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<long>>),    GetType ("[[J"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<float>>),   GetType ("[[F"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaPrimitiveArray<double>>),  GetType ("[[D"));
			Assert.AreEqual (typeof (JavaObjectArray<JavaObjectArray<string>>),     GetType ("[[Ljava/lang/String;"));

			// Yes, these look weird...
			// Assume: class II {}
			Assert.AreEqual (null, GetType ("II"));
			// Assume: package Ljava.lang; class String {}
			Assert.AreEqual (null, GetType ("Ljava/lang/String"));
		}

		[Test]
		public void GetTypeSignature ()
		{
			var jvm = JniRuntime.CurrentRuntime;
			Func<string, Type> GetTypeForSimpleReference = s => {
				var sig     = JniTypeSignature.Parse (s);
				return jvm.TypeManager.GetType (sig);
			};
			Assert.Throws<ArgumentNullException> (() => GetTypeForSimpleReference (null));
			Assert.Throws<ArgumentException> (() => GetTypeForSimpleReference ("foo.bar"));

			Assert.AreEqual (typeof (void),     GetTypeForSimpleReference ("V"));
			Assert.AreEqual (typeof (bool),     GetTypeForSimpleReference ("Z"));
			Assert.AreEqual (typeof (char),     GetTypeForSimpleReference ("C"));
			Assert.AreEqual (typeof (sbyte),    GetTypeForSimpleReference ("B"));
			Assert.AreEqual (typeof (short),    GetTypeForSimpleReference ("S"));
			Assert.AreEqual (typeof (int),      GetTypeForSimpleReference ("I"));
			Assert.AreEqual (typeof (long),     GetTypeForSimpleReference ("J"));
			Assert.AreEqual (typeof (float),    GetTypeForSimpleReference ("F"));
			Assert.AreEqual (typeof (double),   GetTypeForSimpleReference ("D"));
			Assert.AreEqual (typeof (string),   GetTypeForSimpleReference ("java/lang/String"));
			Assert.AreEqual (null,              GetTypeForSimpleReference ("com/example/does/not/exist"));
		}
	}

	class GenericHolder<T> : JavaObject {
		public  const   string  JniTypeName = "com/xamarin/interop/tests/GenericHolder";

		public  T   Value   {get; set;}
	}
}

