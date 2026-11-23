using System;

using NUnit.Framework;

using Android.Runtime;
using Android.Util;

namespace Android.UtilTests {

	[TestFixture]
	[Category ("SparseArrayRepro")]
	public class SparseArrayTests {

		// https://github.com/dotnet/android/issues/13674
		[Test]
		public void JavaCreatedSparseArrayUsesNonGenericPeer ()
		{
			// Keep SparseArray<T> in trimmed builds, so both types map to android/util/SparseArray
			GC.KeepAlive (typeof (SparseArray<Java.Lang.Object>));

			var sparseArrayClass = JNIEnv.FindClass ("android/util/SparseArray");
			try {
				var constructor = JNIEnv.GetMethodID (sparseArrayClass, "<init>", "()V");

				var handle = JNIEnv.NewObject (sparseArrayClass, constructor);
				using (var value = Java.Lang.Object.GetObject<Java.Lang.Object> (handle, JniHandleOwnership.TransferLocalRef)) {
					Assert.AreEqual (typeof (SparseArray), value?.GetType ());
				}

				handle = JNIEnv.NewObject (sparseArrayClass, constructor);
				using (var value = Java.Lang.Object.GetObject<SparseArray> (handle, JniHandleOwnership.TransferLocalRef)) {
					Assert.AreEqual (typeof (SparseArray), value?.GetType ());
				}
			} finally {
				JNIEnv.DeleteGlobalRef (sparseArrayClass);
			}
		}
	}
}
