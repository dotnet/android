using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;

using NUnit.Framework;

namespace Java.LangTests {

	[TestFixture]
	public class ObjectArrayMarshaling {

		[TestCase (0, false)]
		[TestCase (2, false)]
		[TestCase (0, true)]
		[TestCase (2, true)]
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

		[TestCase (0)]
		[TestCase (2)]
		public void ToArrayBoxesPrimitiveElementsForManagedInterfaces (int length)
		{
			var values = new int [length];
			for (int i = 0; i < length; i++)
				values [i] = i + 1;
			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<IConvertible> ());
		}

		[Test]
		public void GetArrayRejectsNonArrayForBoxingDestinations ()
		{
			using var value = new Java.Lang.String ("not an array");
			Assert.Throws<InvalidCastException> (() => value.ToArray<object> ());
			Assert.Throws<InvalidCastException> (() => value.ToArray<IConvertible> ());
			Assert.Throws<InvalidCastException> (() => JNIEnv.GetArray (
				value.Handle, JniHandleOwnership.DoNotTransfer, typeof (object)));
			Assert.Throws<InvalidCastException> (() => JNIEnv.GetArray (
				value.Handle, JniHandleOwnership.DoNotTransfer));
		}

		[TestCase (0)]
		[TestCase (2)]
		public void GetArrayKeepsPrimitiveKindChecksForBoxingSources (int length)
		{
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new int [length]), JniHandleOwnership.TransferLocalRef);
			Assert.Throws<InvalidCastException> (() => array.ToArray<long> ());
			Assert.Throws<InvalidCastException> (() => array.ToArray<string> ());
			Assert.Throws<InvalidCastException> (() => array.ToArray<Java.Lang.String> ());
			Assert.Throws<InvalidCastException> (() => array.ToArray<Java.Lang.Object> ());
			Assert.Throws<InvalidCastException> (() => JNIEnv.GetArray (
				array.Handle, JniHandleOwnership.DoNotTransfer, typeof (string)));
		}

		[TestCase (0)]
		[TestCase (2)]
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

		[TestCase (0)]
		[TestCase (2)]
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

		[Test]
		public void ToArrayPreservesNullObjectArrayElements ()
		{
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [1]), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (new string [1], array.ToArray<string> ());
			CollectionAssert.AreEqual (new Java.Lang.String [1], array.ToArray<Java.Lang.String> ());
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ToArrayRejectsNonStringObjectArrayElements (bool javaStrings)
		{
			using var value = Java.Lang.Integer.ValueOf (42);
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [] { value }), JniHandleOwnership.TransferLocalRef);
			TestDelegate convert = javaStrings
				? () => array.ToArray<Java.Lang.String> ()
				: () => array.ToArray<string> ();
			Assert.Throws<InvalidCastException> (convert);
			int localReferences = Java.Interop.Runtime.LocalReferenceCount;
			for (int i = 0; i < 4; i++)
				Assert.Throws<InvalidCastException> (convert);
			Assert.AreEqual (localReferences, Java.Interop.Runtime.LocalReferenceCount);
		}

		[TestCase (0)]
		[TestCase (2)]
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

		[Test]
		public void ToArrayRejectsNonArrayObjectArrayElements ()
		{
			using var value = new Java.Lang.String ("not an array");
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [] { value }), JniHandleOwnership.TransferLocalRef);
			Assert.Throws<InvalidCastException> (() => array.ToArray<string[]> ());
		}

		[Test]
		[Category ("JniSafetyPreparation")]
		public void ToArrayRejectsNonArrayBeforeNativeArrayAccess ()
		{
			using var value = new Java.Lang.String ("not an array");
			Assert.Throws<InvalidCastException> (() => value.ToArray<string> ());
			Assert.Throws<InvalidCastException> (() => value.ToArray<Java.Lang.Object> ());
		}

		[TestCase (0)]
		[TestCase (2)]
		[Category ("JniSafetyPreparation")]
		public void ToArrayRejectsWrongReferenceArrayKind (int length)
		{
			using var primitiveArray = new Java.Lang.Object (
				JNIEnv.NewArray (new int [length]), JniHandleOwnership.TransferLocalRef);
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<string> ());
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<Java.Lang.Object> ());
			CollectionAssert.AreEqual (new int [length], primitiveArray.ToArray<int> ());
		}

		[Test]
		public void CastJavaLangObjectArrayToByteArrayThrows ()
		{
			using (var objectArray = new Java.Lang.Object (JNIEnv.NewArray (
						new Java.Lang.Object[]{new byte[]{0x1, 0x2, 0x3}}), JniHandleOwnership.TransferLocalRef)) {
				Assert.Throws (typeof (InvalidCastException), () => {
#pragma warning disable 219
						var ignore = (byte[]) objectArray;
#pragma warning restore 219
				});
			}
		}

		[Test]
		public void CastJavaLangObjectToJavaLangObjectArray ()
		{
			using (var objectArray = new Java.Lang.Object (JNIEnv.NewArray (
						new Java.Lang.Object[]{new byte[]{0x1, 0x2, 0x3}}), JniHandleOwnership.TransferLocalRef)) {
				var values = (Java.Lang.Object[]) objectArray;
				Assert.IsNotNull (values);
				Assert.AreEqual (1, values.Length);
				using (var dataObject = values [0]) {
					byte[] data = (byte[]) dataObject;
					Assert.AreEqual (new byte[]{1, 2, 3}, data);
				}
			}
		}
	}
}
