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
		}

		[TestCase (0)]
		[TestCase (2)]
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

