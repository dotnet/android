using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.Interop.PerformanceTests {

	[TestFixture]
	class JavaPrimitiveArraysTiming : Java.InteropTests.JavaVMFixture {

		[Test]
		public void CreationTiming ()
		{
			const int CreationCount = 1000000;
			var a = Stopwatch.StartNew ();
			for (int i = 0; i < CreationCount; ++i) {
				CreateArray ();
			}
			a.Stop ();

			var d = Stopwatch.StartNew ();
			for (int i = 0; i < CreationCount; ++i) {
				CreateDict ();
			}
			d.Stop ();

			Console.WriteLine ($"# {nameof (CreationTiming)}: Array Creation: {a.ElapsedMilliseconds}ms");
			Console.WriteLine ($"# {nameof (CreationTiming)}:  Dict Creation: {d.ElapsedMilliseconds}ms");
		}

		[Test]
		public void LookupTiming ()
		{
			const int LookupCount   = 1000000;
			var av = CreateArray ();
			var a = Stopwatch.StartNew ();
			for (int i = 0; i < LookupCount; ++i) {
				TryArrayLookup (av, typeof (int));
			}
			a.Stop ();
			var bv = CreateDict ();
			var d = Stopwatch.StartNew ();
			for (int i = 0; i < LookupCount; ++i) {
				TryDictLookup (bv, typeof (int));
			}
			d.Stop ();
			Console.WriteLine ($"# {nameof (LookupTiming)}: Array Lookup: {a.ElapsedMilliseconds}ms");
			Console.WriteLine ($"# {nameof (LookupTiming)}:  Dict Lookup: {d.ElapsedMilliseconds}ms");
		}

		static JniPrimitiveArrayInfo_Array[] CreateArray ()
		{
			return new JniPrimitiveArrayInfo_Array[]{
				new ("Z", typeof (Boolean), typeof (Boolean[]), typeof (JavaArray<Boolean>), typeof (JavaPrimitiveArray<Boolean>), typeof (JavaBooleanArray)),
				new ("B", typeof (SByte),   typeof (SByte[]), typeof (JavaArray<SByte>), typeof (JavaPrimitiveArray<SByte>), typeof (JavaSByteArray)),
				new ("C", typeof (Char),    typeof (Char[]), typeof (JavaArray<Char>), typeof (JavaPrimitiveArray<Char>), typeof (JavaCharArray)),
				new ("S", typeof (Int16),   typeof (Int16[]), typeof (JavaArray<Int16>), typeof (JavaPrimitiveArray<Int16>), typeof (JavaInt16Array)),
				new ("I", typeof (Int32),   typeof (Int32[]), typeof (JavaArray<Int32>), typeof (JavaPrimitiveArray<Int32>), typeof (JavaInt32Array)),
				new ("J", typeof (Int64),   typeof (Int64[]), typeof (JavaArray<Int64>), typeof (JavaPrimitiveArray<Int64>), typeof (JavaInt64Array)),
				new ("F", typeof (Single),  typeof (Single[]), typeof (JavaArray<Single>), typeof (JavaPrimitiveArray<Single>), typeof (JavaSingleArray)),
				new ("D", typeof (Double),  typeof (Double[]), typeof (JavaArray<Double>), typeof (JavaPrimitiveArray<Double>), typeof (JavaDoubleArray)),
			};
		}

		static bool TryArrayLookup (JniPrimitiveArrayInfo_Array[] array, Type type)
		{
			foreach (var e in array) {
				if (Array.IndexOf (e.ArrayTypes, type) < 0)
					continue;
				return true;
			}
			return false;
		}

		static Dictionary<Type, JniPrimitiveArrayInfo_Hash> CreateDict ()
		{
			return new Dictionary<Type, JniPrimitiveArrayInfo_Hash>  () {
				[typeof (Boolean)] = new ("Z", typeof (Boolean), typeof (Boolean[]), typeof (JavaArray<Boolean>), typeof (JavaPrimitiveArray<Boolean>), typeof (JavaBooleanArray)),
				[typeof (SByte)  ] = new ("B", typeof (SByte), typeof (SByte[]), typeof (JavaArray<SByte>), typeof (JavaPrimitiveArray<SByte>), typeof (JavaSByteArray)),
				[typeof (Char)   ] = new ("C", typeof (Char), typeof (Char[]), typeof (JavaArray<Char>), typeof (JavaPrimitiveArray<Char>), typeof (JavaCharArray)),
				[typeof (Int16)  ] = new ("S", typeof (Int16), typeof (Int16[]), typeof (JavaArray<Int16>), typeof (JavaPrimitiveArray<Int16>), typeof (JavaInt16Array)),
				[typeof (Int32)  ] = new ("I", typeof (Int32), typeof (Int32[]), typeof (JavaArray<Int32>), typeof (JavaPrimitiveArray<Int32>), typeof (JavaInt32Array)),
				[typeof (Int64)  ] = new ("J", typeof (Int64), typeof (Int64[]), typeof (JavaArray<Int64>), typeof (JavaPrimitiveArray<Int64>), typeof (JavaInt64Array)),
				[typeof (Single) ] = new ("F", typeof (Single), typeof (Single[]), typeof (JavaArray<Single>), typeof (JavaPrimitiveArray<Single>), typeof (JavaSingleArray)),
				[typeof (Double) ] = new ("D", typeof (Double), typeof (Double[]), typeof (JavaArray<Double>), typeof (JavaPrimitiveArray<Double>), typeof (JavaDoubleArray)),
			};
		}

		static bool TryDictLookup (Dictionary<Type, JniPrimitiveArrayInfo_Hash> dict, Type type)
		{
			foreach (var v in dict.Values) {
				if (v.ArrayTypes.Contains (type))
					return true;
			}
			return false;
		}
	}

	readonly struct JniPrimitiveArrayInfo_Array {
		public  readonly    JniTypeSignature    JniTypeSignature;
		public  readonly    Type                PrimitiveType;
		public  readonly    Type[]              ArrayTypes;
		public JniPrimitiveArrayInfo_Array (string jniSimpleReference, Type primitiveType, params Type[] arrayTypes)
		{
			JniTypeSignature    = new JniTypeSignature (jniSimpleReference, arrayRank: 1, keyword: true);
			PrimitiveType       = primitiveType;
			ArrayTypes          = arrayTypes;
		}
	}

	readonly struct JniPrimitiveArrayInfo_Hash {
		public  readonly    JniTypeSignature    JniTypeSignature;
		public  readonly    Type                PrimitiveType;
		public  readonly    HashSet<Type>       ArrayTypes;
		public JniPrimitiveArrayInfo_Hash (string jniSimpleReference, Type primitiveType, params Type[] arrayTypes)
		{
			JniTypeSignature    = new JniTypeSignature (jniSimpleReference, arrayRank: 1, keyword: true);
			PrimitiveType       = primitiveType;
			ArrayTypes          = new (arrayTypes);
		}
	}
}
