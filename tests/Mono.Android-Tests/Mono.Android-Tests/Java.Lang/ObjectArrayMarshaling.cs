using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;

using NUnit.Framework;

namespace Java.LangTests {

	[TestFixture]
	public class ObjectArrayMarshaling {

		[Test]
		public void ToArrayRejectsNonArrayBeforeNativeArrayAccess ()
		{
			using var value = new Java.Lang.String ("not an array");
			Assert.Throws<InvalidCastException> (() => value.ToArray<string> ());
			Assert.Throws<InvalidCastException> (() => value.ToArray<Java.Lang.Object> ());
			Assert.Throws<InvalidCastException> (() => value.ToArray<IJavaObject> ());
			Assert.Throws<InvalidCastException> (() => value.ToArray<Array> ());
			Assert.Throws<InvalidCastException> (() => value.ToArray<IJavaObject[]> ());
			Assert.Throws<InvalidCastException> (() => value.ToArray<Array[]> ());
		}

		[TestCase (0)]
		[TestCase (2)]
		public void ToArrayRejectsWrongReferenceArrayKind (int length)
		{
			using var primitiveArray = new Java.Lang.Object (
				JNIEnv.NewArray (new int [length]), JniHandleOwnership.TransferLocalRef);
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<string> ());
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<Java.Lang.Object> ());
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<IJavaObject> ());
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<Array> ());
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<IJavaObject[]> ());
			Assert.Throws<InvalidCastException> (() => primitiveArray.ToArray<Array[]> ());
			CollectionAssert.AreEqual (new int [length], primitiveArray.ToArray<int> ());
		}

		[TestCase (0)]
		[TestCase (2)]
		public void ToArraySupportsIJavaObjectElements (int length)
		{
			using var value = new Java.Lang.String ("value");
			var values = new Java.Lang.Object [length];
			for (int i = 0; i < length; i++)
				values [i] = value;

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<IJavaObject> ());
		}

		[TestCase (0)]
		[TestCase (2)]
		public void ToArraySupportsJaggedIJavaObjectElements (int length)
		{
			using var value = new Java.Lang.String ("value");
			var values = new Java.Lang.Object [length][];
			for (int i = 0; i < length; i++)
				values [i] = new [] { value };

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<IJavaObject[]> ());
		}

		[TestCase (0)]
		[TestCase (2)]
		public void ToArraySupportsSystemArrayElements (int length)
		{
			var values = new int [length][];
			for (int i = 0; i < length; i++)
				values [i] = new [] { i, 42 };

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<Array> ());
		}

		[TestCase (0)]
		[TestCase (2)]
		public void ToArraySupportsJaggedSystemArrayElements (int length)
		{
			var values = new int [length][][];
			for (int i = 0; i < length; i++)
				values [i] = new [] { new [] { i, 42 } };

			using var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (values, array.ToArray<Array[]> ());
		}

		[TestCase (0)]
		[TestCase (2)]
		public void ToArrayRejectsWrongManagedOnlyArrayRank (int length)
		{
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Object [length]), JniHandleOwnership.TransferLocalRef);
			Assert.Throws<InvalidCastException> (() => array.ToArray<IJavaObject[]> ());
			Assert.Throws<InvalidCastException> (() => array.ToArray<Array[]> ());
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

