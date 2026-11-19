using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.LangTests {

	[TestClass]
	public class ObjectArrayMarshaling {

		[TestMethod]
		[DataRow (0, false)]
		[DataRow (2, false)]
		[DataRow (0, true)]
		[DataRow (2, true)]
		public void GetArrayPreservesPrimitiveArrayConversions (int length, bool generic)
		{
			Array[] sources = [
				length == 0 ? new bool [0] : new [] { true, false },
				length == 0 ? new byte [0] : new byte [] { 1, 255 },
				length == 0 ? new char [0] : new [] { 'a', '\uffff' },
				length == 0 ? new short [0] : new short [] { -1, 32767 },
				length == 0 ? new int [0] : new [] { -1, int.MaxValue },
				length == 0 ? new long [0] : new [] { -1L, long.MaxValue },
				length == 0 ? new float [0] : new [] { -1.5f, 42.5f },
				length == 0 ? new double [0] : new [] { -1.5, 42.5 },
			];
			foreach (var source in sources) {
				using var array = new Java.Lang.Object (JNIEnv.NewArray (source), JniHandleOwnership.TransferLocalRef);
				Array converted = generic
					? array.ToArray<object> ()
					: JNIEnv.GetArray (array.Handle, JniHandleOwnership.DoNotTransfer, typeof (object));
				Assert.IsNotNull (converted);
				Assert.AreEqual (generic ? typeof (object[]) : source.GetType (), converted?.GetType ());
				CollectionAssert.AreEqual (source, converted);
			}
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArrayBoxesPrimitiveElementsForManagedInterfaces (int length)
		{
			var values = new int [length];
			for (int i = 0; i < length; i++)
				values [i] = i + 1;
			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<IConvertible> ());
		}

		[TestMethod]
		public void GetArrayRejectsNonArrayForBoxingDestinations ()
		{
			using var value = new Java.Lang.String ("not an array");
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<object> ());
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<IConvertible> ());
			Assert.ThrowsExactly<InvalidCastException> (() => JNIEnv.GetArray (
				value.Handle, JniHandleOwnership.DoNotTransfer, typeof (object)));
			Assert.ThrowsExactly<InvalidCastException> (() => JNIEnv.GetArray (
				value.Handle, JniHandleOwnership.DoNotTransfer));
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void GetArrayKeepsPrimitiveKindChecksForBoxingSources (int length)
		{
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new int [length]), JniHandleOwnership.TransferLocalRef);
			Assert.ThrowsExactly<InvalidCastException> (() => array.ToArray<long> ());
			Assert.ThrowsExactly<InvalidCastException> (() => array.ToArray<string> ());
			Assert.ThrowsExactly<InvalidCastException> (() => array.ToArray<Java.Lang.String> ());
			Assert.ThrowsExactly<InvalidCastException> (() => array.ToArray<Java.Lang.Object> ());
			Assert.ThrowsExactly<InvalidCastException> (() => JNIEnv.GetArray (
				array.Handle, JniHandleOwnership.DoNotTransfer, typeof (string)));
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArrayConvertsObjectArrayToManagedStrings (int length)
		{
			using var value = new Java.Lang.String ("value");
			var values = new Java.Lang.Object [length];
			var expected = new string [length];
			for (int i = 0; i < length; i++) {
				values [i] = value;
				expected [i] = "value";
			}

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			Assert.AreEqual ("[Ljava/lang/Object;", JNIEnv.GetClassNameFromInstance (array.Handle));
			CollectionAssert.AreEqual (expected, array.ToArray<string> ());
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArrayConvertsObjectArrayToJavaStrings (int length)
		{
			using var value = new Java.Lang.String ("value");
			var values = new Java.Lang.Object [length];
			for (int i = 0; i < length; i++)
				values [i] = value;

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			Assert.AreEqual ("[Ljava/lang/Object;", JNIEnv.GetClassNameFromInstance (array.Handle));
			var converted = array.ToArray<Java.Lang.String> ();
			try {
				CollectionAssert.AreEqual (values, converted);
			} finally {
				if (converted != null) {
					foreach (var item in converted)
						item?.Dispose ();
				}
			}
		}

		[TestMethod]
		public void ToArrayPreservesNullObjectArrayElements ()
		{
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [1]), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (new string [1], array.ToArray<string> ());
			CollectionAssert.AreEqual (new Java.Lang.String [1], array.ToArray<Java.Lang.String> ());
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void ToArrayRejectsNonStringObjectArrayElements (bool javaStrings)
		{
			using var value = Java.Lang.Integer.ValueOf (42);
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [] { value }), JniHandleOwnership.TransferLocalRef);
			Action convert = javaStrings
				? () => array.ToArray<Java.Lang.String> ()
				: () => array.ToArray<string> ();
			Assert.ThrowsExactly<InvalidCastException> (convert);
			int localReferences = Java.Interop.Runtime.LocalReferenceCount;
			for (int i = 0; i < 4; i++)
				Assert.ThrowsExactly<InvalidCastException> (convert);
			Assert.AreEqual (localReferences, Java.Interop.Runtime.LocalReferenceCount);
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArrayConvertsNestedObjectArrayStrings (int length)
		{
			using var value = new Java.Lang.String ("value");
			var values = new Java.Lang.Object [length][];
			var expected = new string [length][];
			for (int i = 0; i < length; i++) {
				values [i] = new Java.Lang.Object [] { value };
				expected [i] = new [] { "value" };
			}

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (expected, array.ToArray<string[]> ());
		}

		[TestMethod]
		public void ToArrayRejectsNonArrayObjectArrayElements ()
		{
			using var value = new Java.Lang.String ("not an array");
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [] { value }), JniHandleOwnership.TransferLocalRef);
			Assert.ThrowsExactly<InvalidCastException> (() => array.ToArray<string[]> ());
		}

		[TestMethod]
		[TestCategory ("JniSafetyPreparation")]
		public void ToArrayRejectsNonArrayBeforeNativeArrayAccess ()
		{
			using var value = new Java.Lang.String ("not an array");
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<string> ());
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<Java.Lang.Object> ());
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<IJavaObject> ());
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<Array> ());
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<IJavaObject[]> ());
			Assert.ThrowsExactly<InvalidCastException> (() => value.ToArray<Array[]> ());
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		[TestCategory ("JniSafetyPreparation")]
		public void ToArrayRejectsWrongReferenceArrayKind (int length)
		{
			using var primitiveArray = new Java.Lang.Object (
				JNIEnv.NewArray (new int [length]), JniHandleOwnership.TransferLocalRef);
			Assert.ThrowsExactly<InvalidCastException> (() => primitiveArray.ToArray<string> ());
			Assert.ThrowsExactly<InvalidCastException> (() => primitiveArray.ToArray<Java.Lang.Object> ());
			Assert.ThrowsExactly<InvalidCastException> (() => primitiveArray.ToArray<IJavaObject> ());
			Assert.ThrowsExactly<InvalidCastException> (() => primitiveArray.ToArray<Array> ());
			Assert.ThrowsExactly<InvalidCastException> (() => primitiveArray.ToArray<IJavaObject[]> ());
			Assert.ThrowsExactly<InvalidCastException> (() => primitiveArray.ToArray<Array[]> ());
			CollectionAssert.AreEqual (new int [length], primitiveArray.ToArray<int> ());
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArraySupportsIJavaObjectElements (int length)
		{
			using var value = new Java.Lang.String ("value");
			var values = new Java.Lang.Object [length];
			for (int i = 0; i < length; i++)
				values [i] = value;

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<IJavaObject> ());
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArraySupportsJaggedIJavaObjectElements (int length)
		{
			using var value = new Java.Lang.String ("value");
			var values = new Java.Lang.Object [length][];
			for (int i = 0; i < length; i++)
				values [i] = new [] { value };

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<IJavaObject[]> ());
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArraySupportsSystemArrayElements (int length)
		{
			var values = new int [length][];
			for (int i = 0; i < length; i++)
				values [i] = new [] { i, 42 };

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<Array> ());
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArraySupportsJaggedSystemArrayElements (int length)
		{
			var values = new int [length][][];
			for (int i = 0; i < length; i++)
				values [i] = new [] { new [] { i, 42 } };

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<Array[]> ());
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (2)]
		public void ToArrayPreservesNullManagedOnlyArrayElements (int length)
		{
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [length]), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (new IJavaObject [length][], array.ToArray<IJavaObject[]> ());
			CollectionAssert.AreEqual (new Array [length][], array.ToArray<Array[]> ());
		}

		[TestMethod]
		public void ToArrayRejectsNonArrayManagedOnlyArrayElements ()
		{
			using var value = new Java.Lang.String ("not an array");
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [] { value }), JniHandleOwnership.TransferLocalRef);
			Assert.ThrowsExactly<InvalidCastException> (() => array.ToArray<IJavaObject[]> ());
			Assert.ThrowsExactly<InvalidCastException> (() => array.ToArray<Array[]> ());
		}

		[TestMethod]
		public void CastJavaLangObjectArrayToByteArrayThrows ()
		{
			using (var objectArray = new Java.Lang.Object (JNIEnv.NewArray (
						new Java.Lang.Object[]{new byte[]{0x1, 0x2, 0x3}}), JniHandleOwnership.TransferLocalRef)) {
				Assert.ThrowsExactly<InvalidCastException> (() => {
#pragma warning disable 219
						var ignore = (byte[]) objectArray;
#pragma warning restore 219
				});
			}
		}

		[TestMethod]
		public void CastJavaLangObjectToJavaLangObjectArray ()
		{
			using (var objectArray = new Java.Lang.Object (JNIEnv.NewArray (
						new Java.Lang.Object[]{new byte[]{0x1, 0x2, 0x3}}), JniHandleOwnership.TransferLocalRef)) {
				var values = (Java.Lang.Object[]) objectArray;
				Assert.IsNotNull (values);
				Assert.AreEqual (1, values.Length);
				using (var dataObject = values [0]) {
					byte[] data = (byte[]) dataObject;
					CollectionAssert.AreEqual (new byte[]{1, 2, 3}, data);
				}
			}
		}
	}
}
