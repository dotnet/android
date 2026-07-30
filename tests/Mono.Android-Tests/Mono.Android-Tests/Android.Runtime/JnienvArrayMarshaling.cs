using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Views;

using Java.Interop;

using NUnit.Framework;

using Java.LangTests;

namespace Android.RuntimeTests {

	[TestFixture]
	public class JnienvArrayMarshaling {

		[Test]
		public void MarshalInt32ArrayArray ()
		{
			var states = new []{
				new[]{1, 2, 3},
				new[]{4, 5, 6},
			};
			var colors = new[]{7, 8};
			var list = new global::Android.Content.Res.ColorStateList (states, colors);
			Assert.AreEqual (7, list.GetColorForState (states [0], Color.Transparent));
			Assert.AreEqual (8, list.GetColorForState (states [1], Color.Transparent));
		}

		[Test]
		public void CopyArray_JavaToSystemByteArray ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = new byte [3];
				JNIEnv.CopyArray (byteArray.Handle, copy, typeof (byte));
				AssertArrays ("CopyArray(Handle, byte[])", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void CopyArray_Byte_JavaToGenericArrayT ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = new byte [3];
				JNIEnv.CopyArray<byte> (byteArray.Handle, copy);
				AssertArrays ("CopyArray<byte>(Handle, byte[])", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void CopyArray_JavaToSystemArray ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = new byte [3];
				JNIEnv.CopyArray (byteArray.Handle, (Array) copy);
				AssertArrays ("CopyArray(Handle, Array)", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void CopyArray_SystemByteArrayToJava ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var orig = new byte[]{ 4, 5, 6 };
				JNIEnv.CopyArray (orig, byteArray.Handle);
				var copy = JNIEnv.GetArray<byte> (byteArray.Handle);
				AssertArrays ("CopyArray(byte[], Handle)", copy, orig);
			}
		}
		
		[Test]
		public void CopyArray_GenericByteArrayToJava ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var orig = new byte[]{ 4, 5, 6 };
				JNIEnv.CopyArray<byte> (orig, byteArray.Handle);
				var copy = JNIEnv.GetArray<byte> (byteArray.Handle);
				AssertArrays ("CopyArray<byte>(byte[], Handle)", copy, orig);
			}
		}

		[Test]
		public void CopyArray_JavaLangStringArrayArrayToSystemStringArrayArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{new[]{"a", "b"}, new[]{"c", "d"}}), JniHandleOwnership.TransferLocalRef)) {
				var values = new[]{new string [2], new string [2]};
				JNIEnv.CopyArray (stringArray.Handle, values);
				AssertArrays ("GetArray<string[]>", values, new string[]{"a", "b"}, new string[]{"c", "d"});
			}
		}

		[Test]
		public void CopyArray_JavaLangObjectArrayToJavaLangStringArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{"a", "b"}), JniHandleOwnership.TransferLocalRef)) {
				Java.Lang.Object[] values = (Java.Lang.Object[]) JNIEnv.GetArray (stringArray.Handle, JniHandleOwnership.DoNotTransfer, typeof(Java.Lang.Object));
				values [0] = new Java.Lang.String ("c");
				JNIEnv.CopyArray (values, stringArray.Handle);
				Assert.AreEqual ("c", JNIEnv.GetArrayItem<string> (stringArray.Handle, 0));
				Assert.AreEqual ("c", JNIEnv.GetArrayItem<Java.Lang.String> (stringArray.Handle, 0));
			}
		}

		[Test]
		public void ByteArrayArray_IsConvertibleTo_JavaLangObjectArray ()
		{
			/*
			 * Yay, Java array covariance allows this:
			 *   byte[][] a = new byte[][]{new byte[]{1,2}};
			 *   Object[] o = a;
			 *   byte[]   c = (byte[]) o [0];
			 */
			IntPtr x = JNIEnv.NewArray<byte[]>(new byte[][]{new byte[]{11, 12}, new byte[]{21, 22}});
			Assert.AreEqual ("[[B", JNIEnv.GetClassNameFromInstance (x));
			var items = JNIEnv.GetArray<Java.Lang.Object>(x);
			JNIEnv.DeleteLocalRef (x);

			Assert.AreEqual (2, items.Length);
			Assert.AreEqual (typeof (Java.Lang.Object), items [0].GetType ());

			var bytes = new byte[2];
			JNIEnv.CopyArray (items [0].Handle, bytes);
			AssertArrays ("CopyArray<byte>", bytes, (byte) 11, (byte) 12);
		}

		[Test]
		public void NewArray_JavaLangString()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[] { new Java.Lang.String ("a"), new Java.Lang.String ("b") }), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual ("[Ljava/lang/String;", JNIEnv.GetClassNameFromInstance (stringArray.Handle));
			}
		}

		[Test]
		public void CopyObjectArray ()
		{
			IntPtr p = JNIEnv.NewObjectArray (new byte[]{1, 2, 3});
			byte[] dest = new byte [3];
			JNIEnv.CopyObjectArray (p, dest);
			AssertArrays ("CopyObjectArray: java->C#", dest, (byte)1, (byte)2, (byte)3);
			dest = new byte[] { 42 };
			JNIEnv.CopyObjectArray (dest, p);
			byte written;
			using (var b = JNIEnv.GetArrayItem<Java.Lang.Byte>(p, 0))
				written = (byte) b.ByteValue ();
			Assert.AreEqual (42, written);
			JNIEnv.DeleteLocalRef (p);
		}

		[Test]
		public void GetArray_Byte ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = JNIEnv.GetArray<byte> (byteArray.Handle);
				AssertArrays ("GetArray<byte>", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void GetArray_ByteArrayArray ()
		{
			byte[][] data = new byte[][]{
				new byte[]{11, 12, 13},
				new byte[]{21, 22, 23},
				new byte[]{31, 32, 33},
			};
			using (var byteArrayArray = new Java.Lang.Object (JNIEnv.NewArray (data), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual ("[[B", JNIEnv.GetClassNameFromInstance (byteArrayArray.Handle));
				byte[][] data2 = JNIEnv.GetArray<byte[]> (byteArrayArray.Handle);
				Assert.AreEqual (data, data2);
				byte[][] data3 = (byte[][]) JNIEnv.GetArray (byteArrayArray.Handle, JniHandleOwnership.DoNotTransfer, typeof (byte[]));
				Assert.AreEqual (data, data3);
				JNIEnv.CopyArray (data3, byteArrayArray.Handle);
			}
		}

		[Test]
		public void GetArray_JavaLangByteArrayToSystemByteArray ()
		{
			var byteObjectArray = new Java.Lang.Byte[]{
				new Java.Lang.Byte (1),
				new Java.Lang.Byte (2),
				new Java.Lang.Byte (3),
			};
			byte[] byteArray = JNIEnv.GetArray<byte>(byteObjectArray);
			AssertArrays ("GetArray: Java.Lang.Byte[]->byte[]", byteArray, (byte) 1, (byte) 2, (byte) 3);
		}

		[Test]
		public void GetArray_JavaLangStringArrayArrayToSystemStringArrayArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{new[]{"a", "b"}, new[]{"c", "d"}}), JniHandleOwnership.TransferLocalRef)) {
				string[][] values = JNIEnv.GetArray<string[]>(stringArray.Handle);
				AssertArrays ("GetArray<string[]>", values, new string[]{"a", "b"}, new string[]{"c", "d"});
			}
		}

		[Test]
		public void GetArray_KeycodeEnum ()
		{
			using (var enumArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{Keycode.A}), JniHandleOwnership.TransferLocalRef)) {
				var copy = JNIEnv.GetArray<Keycode>(enumArray.Handle);
				AssertArrays ("GetArray<Keycode>", copy, Keycode.A);
			}
		}

		[Test]
		public void GetArray_NullableInt32 ()
		{
			var values = new int? [] { 1, null, 3 };
			using (var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual ("[Ljava/lang/Integer;", JNIEnv.GetClassNameFromInstance (array.Handle));

				var copy = JNIEnv.GetArray<int?> (array.Handle);
				AssertArrays ("GetArray<int?>", copy, values);

				Assert.IsNull (JNIEnv.GetArrayItem<int?> (array.Handle, 1));
				JNIEnv.SetArrayItem<int?> (array.Handle, 1, 2);
				Assert.AreEqual ((int?) 2, JNIEnv.GetArrayItem<int?> (array.Handle, 1));
			}
		}

		[Test]
		public void GetArray_NullableByte ()
		{
			var values = new byte? [] { 1, null, 200 };
			using (var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual ("[Ljava/lang/Byte;", JNIEnv.GetClassNameFromInstance (array.Handle));

				var copy = JNIEnv.GetArray<byte?> (array.Handle);
				AssertArrays ("GetArray<byte?>", copy, values);

				Assert.IsNull (JNIEnv.GetArrayItem<byte?> (array.Handle, 1));
				JNIEnv.SetArrayItem<byte?> (array.Handle, 1, 255);
				Assert.AreEqual ((byte?) 255, JNIEnv.GetArrayItem<byte?> (array.Handle, 1));

				var replacement = new byte? [] { 128, 129, null };
				JNIEnv.CopyArray (replacement, array.Handle);
				AssertArrays ("CopyArray<byte?>", JNIEnv.GetArray<byte?> (array.Handle), replacement);
			}
		}

		[Test]
		[Category ("NativeAOTTrimmable")]
		public void GetArray_NullableByteArrayArray ()
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("Test only relevant for the trimmable typemap path.");
			}

			var values = new [] {
				new byte? [] { 1, null, 200 },
				new byte? [] { 255, 128 },
			};
			using (var array = new Java.Lang.Object (JNIEnv.NewArray (values), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual ("[[Ljava/lang/Byte;", JNIEnv.GetClassNameFromInstance (array.Handle));

				var copy = JNIEnv.GetArray<byte?[]> (array.Handle);
				Assert.AreEqual (values.Length, copy.Length);
				for (int i = 0; i < values.Length; i++) {
					AssertArrays ($"GetArray<byte?[]>[{i}]", copy [i], values [i]);
				}
			}
		}

		[Test]
		public void GetArray_JavaLangStringArrayToJavaLangObjectArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{"a", "b"}), JniHandleOwnership.TransferLocalRef)) {
				Java.Lang.Object[] values = (Java.Lang.Object[]) JNIEnv.GetArray (stringArray.Handle, JniHandleOwnership.DoNotTransfer, typeof(Java.Lang.Object));
				Assert.AreEqual (2, values.Length);
				Assert.AreEqual (typeof(Java.Lang.String), values [0].GetType ());
				Assert.AreEqual ("a", values [0].ToString ());
				Assert.AreEqual (typeof(Java.Lang.String), values [1].GetType ());
				Assert.AreEqual ("b", values [1].ToString ());
			}
		}

		[Test]
		public void GetArrayItem ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual (2, JNIEnv.GetArrayItem<byte> (byteArray.Handle, 1));
				JNIEnv.SetArrayItem (byteArray.Handle, 1, (byte) 42);
				Assert.AreEqual (42, JNIEnv.GetArrayItem<byte> (byteArray.Handle, 1));
			}
		}

		[Test]
		public void GetArrayItem_Int32ArrayArray ()
		{
			IntPtr array = JNIEnv.NewObjectArray (1, Java.Lang.Class.Object);
			Assert.AreEqual ("[Ljava/lang/Object;", JNIEnv.GetClassNameFromInstance (array));
			int[] seq = new int[]{1, 2, 3};
			JNIEnv.SetArrayItem (array, 0, seq);
			int[] oArray = JNIEnv.GetArrayItem<int[]> (array, 0);
			AssertArrays ("GetArrayItem_Int32ArrayArray", seq, oArray);
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		public void SetArrayItem ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				JNIEnv.SetArrayItem (byteArray.Handle, 1, (byte) 42);

				var copy = new byte [3];
				JNIEnv.CopyArray (byteArray.Handle, copy);
				AssertArrays ("CopyArray<byte>", copy, (byte) 1, (byte) 42, (byte) 3);
			}
		}

		[Test]
		public void SetArrayItem_JavaLangString ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{"a", "b"}), JniHandleOwnership.TransferLocalRef)) {
				using (var v = new Java.Lang.String ("d"))
					JNIEnv.SetArrayItem (stringArray.Handle, 1, v);
				Assert.AreEqual ("d", JNIEnv.GetArrayItem<string> (stringArray.Handle, 1));
			}
		}

		[Test]
		[Category ("JNIObjectArray")]
		public void GetObjectArray ()
		{
			var context = Application.Context;
			// Sample the registry at each step, so a failure says *when* `Application.Context`
			// stopped being the registered peer rather than only that it did.
			var atEntry = ProbeContextPeer (context);
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				object[] data = JNIEnv.GetObjectArray (byteArray.Handle, new[]{typeof (byte), typeof (byte), typeof (byte)});
				AssertArrays ("GetObjectArray", data, (object) 1, (object) 2, (object) 3);
			}
			var beforeNewArray = ProbeContextPeer (context);
			using (var objectArray =
					new Java.Lang.Object (
							JNIEnv.NewArray (
								new Java.Lang.Object[]{context, 42L, "string"},
								typeof (Java.Lang.Object)),
						JniHandleOwnership.TransferLocalRef)) {
				var afterNewArray = ProbeContextPeer (context);
				object[] values = JNIEnv.GetObjectArray (objectArray.Handle, new[]{typeof(Context), typeof (int)});
				Assert.AreEqual (3, values.Length);
				// Deliberately not `Assert.AreSame()`: this intermittently fails on CoreCLR
				// (dotnet/android#10973), and both peers render identically, so the default
				// message tells us nothing. Only build the diagnostic when it actually fails.
				if (!ReferenceEquals (context, values [0]))
					Assert.Fail (DescribeContextPeerMismatch (context, values [0], atEntry, beforeNewArray, afterNewArray));
				Assert.IsInstanceOf<int> (values [1], $"Expected converted Int32, got {values [1]?.GetType ()}: {values [1]}.");
				Assert.AreEqual (42, (int)values [1]);
				Assert.AreEqual ("string", values [2].ToString ());
			}
		}

		// What the registry reports for `Application.Context` at one point in time. Records a
		// description rather than the peer itself: retaining the peer would keep it alive and
		// could perturb the very GC behaviour being investigated.
		readonly struct PeerProbe {

			readonly bool   isExpected;
			readonly string description;

			public PeerProbe (bool isExpected, string description)
			{
				this.isExpected  = isExpected;
				this.description = description;
			}

			public override string ToString ()
				=> $"{description} (is expected: {isExpected})";
		}

		static PeerProbe ProbeContextPeer (Context expected)
		{
			if (!expected.PeerReference.IsValid)
				return new PeerProbe (false, "<invalid PeerReference>");
			var peeked = JniRuntime.CurrentRuntime.ValueManager.PeekPeer (expected.PeerReference);
			return new PeerProbe (ReferenceEquals (peeked, expected), Describe (peeked));
		}

		// `GetObjectArray()` should hand back the *same* managed peer that `Application.Context`
		// holds, because `JniValueManager.GetPeer()` peeks the registry before creating a new peer.
		// When that fails, the Java instance is the same but the managed peers differ, so dump
		// enough of the registry to tell *why* they disagree: whether the entry was replaced by a
		// second `AddPeer()`, evicted entirely, or its weak reference was cleared.
		static string DescribeContextPeerMismatch (Context expected, object actual, PeerProbe atEntry, PeerProbe beforeNewArray, PeerProbe afterNewArray)
		{
			var sb = new StringBuilder ();
			sb.AppendLine ("Expected `Application.Context` and `GetObjectArray ()[0]` to be the same managed peer.");
			AppendPeer (sb, "expected (Application.Context)", expected);
			AppendPeer (sb, "actual   (GetObjectArray ()[0])", actual);

			// The registry over time. If the peer was already wrong `atEntry`, something evicted
			// or replaced it *before* this test ran and the marshaling is only the messenger.
			sb.AppendLine ("  PeekPeer (Application.Context) over time:");
			sb.AppendLine ($"    at test entry:      {atEntry}");
			sb.AppendLine ($"    before NewArray:    {beforeNewArray}");
			sb.AppendLine ($"    after NewArray:     {afterNewArray}");
			sb.AppendLine ($"  Registry first diverged: {ContextPeerWatchAttribute.DivergedAfter ?? "<not observed>"}");
			// Set by JavaMarshalRegisteredPeers: the registry can only disagree with a cached
			// `Application.Context` if a second managed peer was created for the same Java
			// instance, so report how that happened.
			sb.AppendLine ($"  CollectPeers() evicted live peers:  {AppContext.GetData ("Microsoft.Android.Runtime.LivePeerEvictions") ?? "<none>"}");
			sb.AppendLine ($"  AddPeer() replaced registrations:   {AppContext.GetData ("Microsoft.Android.Runtime.PeerReplacements") ?? "<none>"}");
			sb.AppendLine ($"  AddPeer() appended duplicates:      {AppContext.GetData ("Microsoft.Android.Runtime.PeerDuplicateAppends") ?? "<none>"}");

			// Did `Application.Context` itself change after we captured it?
			sb.AppendLine ($"  Application.Context still == expected: {ReferenceEquals (Application.Context, expected)}");

			if (actual is IJavaPeerable actualPeer && expected.PeerReference.IsValid && actualPeer.PeerReference.IsValid)
				sb.AppendLine ($"  IsSameObject (expected, actual): {JniEnvironment.Types.IsSameObject (expected.PeerReference, actualPeer.PeerReference)}");

			var manager = JniRuntime.CurrentRuntime.ValueManager;

			// If this returns `actual`, the registry entry was replaced out from under
			// `Application.Context`; if it returns null, the entry was evicted or collected.
			var peeked = expected.PeerReference.IsValid ? manager.PeekPeer (expected.PeerReference) : null;
			sb.AppendLine ($"  PeekPeer (expected.PeerReference) => {Describe (peeked)}");
			sb.AppendLine ($"    is expected: {ReferenceEquals (peeked, expected)}; is actual: {ReferenceEquals (peeked, actual)}");

			sb.AppendLine ($"  Surfaced peers with JniIdentityHashCode=0x{expected.JniIdentityHashCode:x}:");
			foreach (var info in manager.GetSurfacedPeers ()) {
				if (info.JniIdentityHashCode != expected.JniIdentityHashCode)
					continue;
				info.SurfacedPeer.TryGetTarget (out var target);
				sb.AppendLine ($"    {Describe (target)} (is expected: {ReferenceEquals (target, expected)}; is actual: {ReferenceEquals (target, actual)})");
			}
			return sb.ToString ();
		}

		static void AppendPeer (StringBuilder sb, string label, object value)
		{
			sb.AppendLine ($"  {label}: {Describe (value)}");
			if (value is not IJavaPeerable peer)
				return;
			sb.AppendLine ($"    JniIdentityHashCode=0x{peer.JniIdentityHashCode:x} PeerReference={peer.PeerReference} JniManagedPeerState={peer.JniManagedPeerState}");
		}

		// The Java-side `toString()` is identical for every peer over the same instance, so
		// identify peers by their *managed* hash code instead.
		static string Describe (object value)
			=> value == null
				? "<null>"
				: $"{value.GetType ().FullName}@managed-0x{RuntimeHelpers.GetHashCode (value):x}";

		[Test]
		public void NewArray_Int32ArrayArray ()
		{
			IntPtr x = JNIEnv.NewArray<int[]>(new int[][]{new[]{11, 12}, new []{21, 22}});
			string t = JNIEnv.GetClassNameFromInstance (x);
			JNIEnv.DeleteLocalRef (x);
			Assert.AreEqual ("[[I", t);
		}

		[Test]
		public void NewArray_Int32ArrayArray_ArrayOverload ()
		{
			Array array = new int[][]{new[]{11, 12}, new []{21, 22}};
			IntPtr x = JNIEnv.NewArray(array);
			string t = JNIEnv.GetClassNameFromInstance (x);
			JNIEnv.DeleteLocalRef (x);
			Assert.AreEqual ("[[I", t);
		}

		// http://bugzilla.xamarin.com/show_bug.cgi?id=12479
		[Test]
		public void NewArray_Int32ArrayArray_ShouldNotLeak ()
		{
			int[][] array = new int[][]{
				new int[]{1,2,3,4},
				new int[]{5,6,7,8},
			};

			// 600 chosen as LREF table is 512 entries, so if this leaks it should overflow
			for (int i = 0; i < 600; ++i) {
				IntPtr l = JNIEnv.NewArray (array);
				JNIEnv.DeleteLocalRef (l);
			}
		}

		[Test]
		public void NewArray_UseJcwTypeWhenRenamed ()
		{
			IntPtr lref = JNIEnv.NewArray<CreateInstance_OverrideAbsListView_Adapter>(new CreateInstance_OverrideAbsListView_Adapter[0]);
			Assert.AreEqual (
					"[Lcom/xamarin/android/runtimetests/CreateInstance_OverrideAbsListView_Adapter;",
					JNIEnv.GetClassNameFromInstance (lref));
			JNIEnv.DeleteLocalRef (lref);
		}

		[Test]
		public void NewObjectArray_SystemByteArrayToJavaLangByteArray ()
		{
			IntPtr p = JNIEnv.NewObjectArray (new byte[]{1, 2, 3});
			string t = JNIEnv.GetClassNameFromInstance (p);
			JNIEnv.DeleteLocalRef (p);
			Assert.AreEqual ("[Ljava/lang/Byte;", t);
		}

		// http://bugzilla.xamarin.com/show_bug.cgi?id=360
		[Test]
		public void BoundArrayPropertiesHaveSetters ()
		{
			using (var opt = new BitmapFactory.Options ()) {
				opt.InTempStorage = new byte [] {1, 3, 5};
				var inTempStorage = opt.InTempStorage;
				Assert.AreEqual (3, inTempStorage.Count);
				AssertArrays ("BoundArrayPropertiesHaveSetters", inTempStorage, (byte) 1, (byte) 3, (byte) 5);
				Assert.DoesNotThrow (() => ((IDisposable)inTempStorage).Dispose ());
			}
		}

		static void AssertArrays<T> (string message, IList<T> actual, params T[] expected)
		{
			Assert.AreEqual (expected.Length, actual.Count, message);
			for (int i = 0; i < expected.Length; ++i)
				Assert.AreEqual (expected [i], actual [i], message);
		}
	}
}
