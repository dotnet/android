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
		[Category ("JniSafetyPreparation")]
		public void ToArrayPreservesReferenceCovarianceAndNullElements ()
		{
			var expected = new string [] { "first", null, "second" };
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (expected, typeof (string)), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (expected, array.ToArray<string> ());
			var peers = array.ToArray<Java.Lang.String> ();
			if (peers is null)
				throw new InvalidOperationException ("Expected a managed reference array.");
			try {
				CollectionAssert.AreEqual (expected, Array.ConvertAll (peers, peer => peer?.ToString ()));
			} finally {
				foreach (var peer in peers)
					peer?.Dispose ();
			}

			using var first = Java.Lang.Integer.ValueOf (11);
			using var second = Java.Lang.Integer.ValueOf (22);
			using var integers = new Java.Lang.Object (
				JNIEnv.NewArray (new Java.Lang.Integer [] { first, null, second }), JniHandleOwnership.TransferLocalRef);
			var objects = integers.ToArray<Java.Lang.Object> ();
			if (objects is null)
				throw new InvalidOperationException ("Expected a covariant managed object array.");
			Assert.AreSame (first, objects [0]);
			Assert.IsNull (objects [1]);
			Assert.AreSame (second, objects [2]);
		}

		[Test]
		[Category ("JniSafetyPreparation")]
		public void ToArrayPreservesNullablePrimitiveElements ()
		{
			var expected = new int? [] { int.MinValue, null, int.MaxValue };
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (expected, typeof (int?)), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (expected, array.ToArray<int?> ());
		}

		[Test]
		[Category ("JniSafetyPreparation")]
		public void ToArrayPreservesByteBitsAndDisposedNullResult ()
		{
			using var array = new Java.Lang.Object (
				JNIEnv.NewArray (new byte [] { 0, 127, 128, 255 }), JniHandleOwnership.TransferLocalRef);
			CollectionAssert.AreEqual (new byte [] { 0, 127, 128, 255 }, array.ToArray<byte> ());
			array.Dispose ();
			Assert.IsNull (array.ToArray<byte> ());
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
