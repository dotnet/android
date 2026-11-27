using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Android.Runtime;
using Android.Util;
using Java.Interop;

namespace Android.UtilTests {

	[TestClass]
	[TestCategory ("HandwrittenJniBindings")]
	public class SparseArrayTests {

		[TestMethod]
		public void GenericSparseArrayUsesSparseArrayClass ()
		{
			using var values = new SparseArray<string> ();

			values.Put (1, "one");

			Assert.AreEqual ("one", values.Get (1));
		}

		[TestMethod]
		public void GenericAppendDispatchesToJavaOverride ()
		{
			using var values = new JavaOverrideSparseArray ();
			((SparseArray<string>) values).Append (1, "one");
			Assert.AreEqual (1, values.AppendedKey);
		}

		[TestMethod]
		public void GenericManagedBaseCallUsesJavaBaseImplementation ()
		{
			using var values = new ManagedDerivedSparseArray ();
			values.Append (2, "base");
			Assert.AreEqual ("base", values.Get (2));
			Assert.IsFalse (values.NonGenericAppendCalled);
		}

		// https://github.com/dotnet/android/issues/13674
		[TestMethod]
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

		[Register ("net/dot/android/test/JavaOverrideSparseArray")]
		public class JavaOverrideSparseArray : SparseArray<string> {

			static readonly JniPeerMembers members = new JniPeerMembers (
				"net/dot/android/test/JavaOverrideSparseArray", typeof (JavaOverrideSparseArray));

			// Give the generated Java subclass its own peer metadata.
			public override JniPeerMembers JniPeerMembers => members;
			public int AppendedKey { get; private set; } = -1;

			public override void Append (int key, Java.Lang.Object? value) => AppendedKey = key;
		}

		public class ManagedDerivedSparseArray : SparseArray<string> {
			public bool NonGenericAppendCalled { get; private set; }

			public override void Append (int key, Java.Lang.Object? value) => NonGenericAppendCalled = true;
			public override void Append (int key, string value) => base.Append (key, value);
		}
	}
}
