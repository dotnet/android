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
		public void GenericMethodsDispatchToJavaOverrides ()
		{
			using var values = new JavaOverrideSparseArray ();
			SparseArray<string> generic = values;

			generic.Append (1, "one");
			Assert.AreEqual ("append", values.Called);
			generic.Put (1, "one");
			Assert.AreEqual ("put", values.Called);
			generic.SetValueAt (0, "one");
			Assert.AreEqual ("setValueAt", values.Called);
			Assert.AreEqual (42, generic.IndexOfValue ("one"));
			Assert.AreEqual ("get", generic.Get (1));
			Assert.AreEqual ("getOrDefault", generic.Get (1, "default"));
			Assert.AreEqual ("valueAt", generic.ValueAt (0));
		}

		[TestMethod]
		public void GenericManagedBaseCallUsesJavaBaseImplementation ()
		{
			using var values = new ManagedDerivedSparseArray ();
			values.CallBaseAppend (2, "base");
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
			public string Called { get; private set; }

			public override void Append (int key, Java.Lang.Object value) => Called = "append";
			public override void Put (int key, Java.Lang.Object value) => Called = "put";
			public override void SetValueAt (int index, Java.Lang.Object value) => Called = "setValueAt";
			public override int IndexOfValue (Java.Lang.Object value) => 42;
			public override Java.Lang.Object Get (int key, Java.Lang.Object valueIfKeyNotFound) => new Java.Lang.String ("getOrDefault");

			// SparseArray<E> hides the non-generic get(int) and valueAt(int), so export their Java overrides.
			[Export ("get")]
			public Java.Lang.Object JavaGet (int key) => new Java.Lang.String ("get");

			[Export ("valueAt")]
			public Java.Lang.Object JavaValueAt (int index) => new Java.Lang.String ("valueAt");
		}

		public class ManagedDerivedSparseArray : SparseArray<string> {
			public bool NonGenericAppendCalled { get; private set; }

			public override void Append (int key, Java.Lang.Object value) => NonGenericAppendCalled = true;
			public void CallBaseAppend (int key, string value) => base.Append (key, value);
		}
	}
}
