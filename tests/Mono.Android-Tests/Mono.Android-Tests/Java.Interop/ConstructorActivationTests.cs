using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class ConstructorActivationTests
	{
		[Test]
		public void JavaSideDefaultConstructorRunsOnceAndRegistersPeer ()
		{
			ConstructorActivationDefault.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationDefault> ("()V")) {
				Assert.IsNotNull (instance);
				Assert.AreEqual (1, ConstructorActivationDefault.ConstructorInvocations);
				Assert.AreEqual (1, instance.ConstructorOrdinal);
				AssertRegisteredSame (instance);
			}
		}

		[Test]
		public void ManagedConstructionDoesNotReenterThroughJavaConstructorActivation ()
		{
			ConstructorActivationDefault.Reset ();

			using (var instance = new ConstructorActivationDefault ()) {
				Assert.IsNotNull (instance);
				Assert.AreEqual (1, ConstructorActivationDefault.ConstructorInvocations);
				Assert.AreEqual (1, instance.ConstructorOrdinal);
				AssertRegisteredSame (instance);
			}
		}

		[Test]
		public void CreateInstanceDoesNotActivateManagedConstructorInsideNewObjectScope ()
		{
			ConstructorActivationDefault.Reset ();

			IntPtr handle = JNIEnv.CreateInstance (typeof (ConstructorActivationDefault), "()V");
			try {
				var peer = JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (handle));

				Assert.AreEqual (0, ConstructorActivationDefault.ConstructorInvocations);
				Assert.IsNull (peer);
			} finally {
				JNIEnv.DeleteLocalRef (handle);
			}
		}

		[Test]
		[Category ("InheritedActivationCreateInstance")]
		public void CreateInstanceWithInheritedActivationCtorDoesNotRegisterPeerInsideNewObjectScope ()
		{
			ConstructorActivationMarshalObject.Reset ();

			IntPtr handle = JNIEnv.CreateInstance (typeof (ConstructorActivationMarshalObject), "(Z)V", new JValue (true));
			try {
				var peer = JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (handle));

				Assert.AreEqual (0, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNull (peer);
			} finally {
				JNIEnv.DeleteLocalRef (handle);
			}
		}

		[Test]
		public void JavaSideConstructorReinvokesExistingActivatablePeer ()
		{
			ConstructorActivationDefault.Reset ();

			using (var instance = new ConstructorActivationDefault ()) {
				var peer = (IJavaPeerable) instance;
				peer.SetJniManagedPeerState (instance.JniManagedPeerState | JniManagedPeerStates.Activatable | JniManagedPeerStates.Replaceable);

				JNIEnv.FinishCreateInstance (instance.Handle, "()V");

				Assert.AreEqual (2, ConstructorActivationDefault.ConstructorInvocations);
				Assert.AreEqual (2, instance.ConstructorOrdinal);
				AssertRegisteredSame (instance);
			}
		}

		[Test]
		public void JavaSideThrowingConstructorPropagatesException ()
		{
			ConstructorActivationThrowing.Reset ();

			var exception = Assert.Catch<Exception> (() => CreateFromJavaExpectingConstructorException<ConstructorActivationThrowing> ("()V"));
			Assert.IsNotNull (exception);
			Assert.AreEqual (1, ConstructorActivationThrowing.ConstructorInvocations);
			Assert.IsTrue (exception.ToString ().Contains (ConstructorActivationThrowing.ExceptionMessage));
		}

		[Test]
		public void JavaSideContextConstructorForwardsArgument ()
		{
			ConstructorActivationContextView.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationContextView> (
					"(Landroid/content/Context;)V",
					new JValue (Application.Context))) {
				Assert.AreEqual (1, ConstructorActivationContextView.ConstructorInvocations);
				Assert.IsNotNull (instance.ContextValue);
				AssertSameJavaObject (Application.Context, instance.ContextValue);
			}
		}

		[Test]
		public void JavaSideContextAttributeSetConstructorForwardsArguments ()
		{
			ConstructorActivationAttributeSetView.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationAttributeSetView> (
					"(Landroid/content/Context;Landroid/util/AttributeSet;)V",
					new JValue (Application.Context),
					JValue.Zero)) {
				Assert.AreEqual (1, ConstructorActivationAttributeSetView.ConstructorInvocations);
				Assert.IsNotNull (instance.ContextValue);
				AssertSameJavaObject (Application.Context, instance.ContextValue);
				Assert.IsNull (instance.AttributeSetValue);
			}
		}

		[Test]
		public void JavaSideContextAttributeSetStyleConstructorForwardsArguments ()
		{
			ConstructorActivationStyledView.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationStyledView> (
					"(Landroid/content/Context;Landroid/util/AttributeSet;I)V",
					new JValue (Application.Context),
					JValue.Zero,
					new JValue (42))) {
				Assert.AreEqual (1, ConstructorActivationStyledView.ConstructorInvocations);
				Assert.IsNotNull (instance.ContextValue);
				AssertSameJavaObject (Application.Context, instance.ContextValue);
				Assert.IsNull (instance.AttributeSetValue);
				Assert.AreEqual (42, instance.DefStyleAttrValue);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		public void JavaSideBooleanConstructorForwardsTrue ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(Z)V", new JValue (true))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (true, instance.BooleanValue);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		public void JavaSideBooleanConstructorForwardsFalse ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(Z)V", new JValue (false))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (false, instance.BooleanValue);
			}
		}

		[Test]
		public void JavaSideByteConstructorForwardsSignedValue ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(B)V", new JValue ((sbyte) -12))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual ((sbyte) -12, instance.ByteValue);
			}
		}

		[Test]
		public void JavaSideCharConstructorForwardsValue ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(C)V", new JValue ('Q'))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual ('Q', instance.CharValue);
			}
		}

		[Test]
		public void JavaSideShortConstructorForwardsValue ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(S)V", new JValue ((short) -1234))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual ((short) -1234, instance.ShortValue);
			}
		}

		[Test]
		public void JavaSideIntConstructorForwardsValue ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(I)V", new JValue (0x1234567))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (0x1234567, instance.IntValue);
			}
		}

		[Test]
		public void JavaSideLongConstructorForwardsValue ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(J)V", new JValue (0x123456789L))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (0x123456789L, instance.LongValue);
			}
		}

		[Test]
		public void JavaSideFloatConstructorForwardsValue ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(F)V", new JValue (12.5f))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (12.5f, instance.FloatValue);
			}
		}

		[Test]
		public void JavaSideDoubleConstructorForwardsValue ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(D)V", new JValue (-42.25))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (-42.25, instance.DoubleValue);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		public void JavaSidePrimitiveMixedConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> (
					"(IZJ)V",
					new JValue (12345),
					new JValue (true),
					new JValue (9876543210L))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (12345, instance.MultiIntValue);
				Assert.AreEqual (true, instance.MultiBooleanValue);
				Assert.AreEqual (9876543210L, instance.MultiLongValue);
			}
		}

		[Test]
		public void JavaSideStringConstructorForwardsValue ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var value = new Java.Lang.String ("hello constructor"))
			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(Ljava/lang/String;)V", new JValue (value))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual ("hello constructor", instance.StringValue);
			}
		}

		[Test]
		public void JavaSideStringConstructorForwardsNull ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("(Ljava/lang/String;)V", JValue.Zero)) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNull (instance.StringValue);
			}
		}

		[Test]
		public void JavaSideTwoStringConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var first = new Java.Lang.String ("first"))
			using (var second = new Java.Lang.String ("second"))
			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> (
					"(Ljava/lang/String;Ljava/lang/String;)V",
					new JValue (first),
					new JValue (second))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual ("first", instance.FirstStringValue);
				Assert.AreEqual ("second", instance.SecondStringValue);
			}
		}

		[Test]
		public void JavaSideTwoStringConstructorForwardsNullSecondValue ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var first = new Java.Lang.String ("first"))
			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> (
					"(Ljava/lang/String;Ljava/lang/String;)V",
					new JValue (first),
					JValue.Zero)) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual ("first", instance.FirstStringValue);
				Assert.IsNull (instance.SecondStringValue);
			}
		}

		[Test]
		public void JavaSideStringIntConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var text = new Java.Lang.String ("string-int"))
			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> (
					"(Ljava/lang/String;I)V",
					new JValue (text),
					new JValue (17))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual ("string-int", instance.StringIntStringValue);
				Assert.AreEqual (17, instance.StringIntValue);
			}
		}

		[Test]
		public void JavaSideIntArrayConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([I)V",
					JNIEnv.NewArray (new [] { 1, 2, 3, 5 }))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (new [] { 1, 2, 3, 5 }, instance.IntArrayValue);
			}
		}

		[Test]
		public void JavaSideIntArrayConstructorForwardsEmptyArray ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([I)V",
					JNIEnv.NewArray (Array.Empty<int> ()))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNotNull (instance.IntArrayValue);
				Assert.AreEqual (0, instance.IntArrayValue.Length);
			}
		}

		[Test]
		public void JavaSideIntArrayConstructorForwardsNull ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("([I)V", JValue.Zero)) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNull (instance.IntArrayValue);
			}
		}

		[Test]
		public void JavaSideStringIntArrayConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var label = new Java.Lang.String ("string-array"))
			{
				IntPtr array = JNIEnv.NewArray (new [] { 8, 13, 21 });
				try {
					using (var instance = CreateFromJava<ConstructorActivationMarshalObject> (
							"(Ljava/lang/String;[I)V",
							new JValue (label),
							new JValue (array))) {
						Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
						Assert.AreEqual ("string-array", instance.StringArrayLabel);
						Assert.AreEqual (new [] { 8, 13, 21 }, instance.StringArrayValues);
					}
				} finally {
					JNIEnv.DeleteLocalRef (array);
				}
			}
		}

		[Test]
		public void JavaSideBooleanArrayConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([Z)V",
					JNIEnv.NewArray (new [] { true, false, true }))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (new [] { true, false, true }, instance.BooleanArrayValue);
			}
		}

		[Test]
		public void JavaSideByteArrayConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([B)V",
					JNIEnv.NewArray (new byte [] { 1, 127, 255 }))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (new byte [] { 1, 127, 255 }, instance.ByteArrayValue);
			}
		}

		[Test]
		public void JavaSideStringArrayConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([Ljava/lang/String;)V",
					JNIEnv.NewArray (new [] { "red", "green", "blue" }))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (new [] { "red", "green", "blue" }, instance.StringArrayValue);
			}
		}

		[Test]
		public void JavaSideStringArrayConstructorForwardsNullElement ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([Ljava/lang/String;)V",
					JNIEnv.NewArray (new [] { "red", null, "blue" }))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.AreEqual (new [] { "red", null, "blue" }, instance.StringArrayValue);
			}
		}

		[Test]
		public void JavaSideStringArrayConstructorForwardsNull ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("([Ljava/lang/String;)V", JValue.Zero)) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNull (instance.StringArrayValue);
			}
		}

		[Test]
		public void JavaSideObjectArrayConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var first = new Java.Lang.String ("object-array-value"))
			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([Ljava/lang/Object;)V",
					JNIEnv.NewArray (new Java.Lang.Object [] { first, Application.Context }))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNotNull (instance.ObjectArrayValue);
				Assert.AreEqual (2, instance.ObjectArrayValue.Length);
				Assert.AreEqual ("object-array-value", instance.ObjectArrayValue [0].ToString ());
				AssertSameJavaObject (Application.Context, instance.ObjectArrayValue [1]);
			}
		}

		[Test]
		public void JavaSideObjectArrayConstructorForwardsNull ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJava<ConstructorActivationMarshalObject> ("([Ljava/lang/Object;)V", JValue.Zero)) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNull (instance.ObjectArrayValue);
			}
		}

		[Test]
		public void JavaSideNestedIntArrayConstructorForwardsValues ()
		{
			ConstructorActivationMarshalObject.Reset ();
			AssumeTrimmableConstructorParameterMarshalling ();

			using (var instance = CreateFromJavaWithLocalArray<ConstructorActivationMarshalObject> (
					"([[I)V",
					JNIEnv.NewArray<int[]> (new [] {
						new [] { 1, 2 },
						new [] { 3, 4, 5 },
					}))) {
				Assert.AreEqual (1, ConstructorActivationMarshalObject.ConstructorInvocations);
				Assert.IsNotNull (instance.NestedIntArrayValue);
				Assert.AreEqual (new [] { 1, 2 }, instance.NestedIntArrayValue [0]);
				Assert.AreEqual (new [] { 3, 4, 5 }, instance.NestedIntArrayValue [1]);
			}
		}

		static void AssumeTrimmableConstructorParameterMarshalling ()
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("Legacy TypeManager.n_Activate does not marshal string, short, or array constructor parameters; this case validates trimmable constructor UCO parameter marshalling.");
			}
		}

		static T CreateFromJava<T> (string constructorSignature, params JValue [] arguments)
			where T : Java.Lang.Object
		{
			var instance = JNIEnv.StartCreateInstance (typeof (T), constructorSignature, arguments);
			JNIEnv.FinishCreateInstance (instance, constructorSignature, arguments);
			var result = Java.Lang.Object.GetObject<T> (instance, JniHandleOwnership.TransferLocalRef);
			Assert.IsNotNull (result);
			return result;
		}

		static T CreateFromJavaWithLocalArray<T> (string constructorSignature, IntPtr array)
			where T : Java.Lang.Object
		{
			try {
				return CreateFromJava<T> (constructorSignature, new JValue (array));
			} finally {
				JNIEnv.DeleteLocalRef (array);
			}
		}

		static void CreateFromJavaExpectingConstructorException<T> (string constructorSignature, params JValue [] arguments)
			where T : Java.Lang.Object
		{
			IntPtr instance = IntPtr.Zero;
			try {
				instance = JNIEnv.StartCreateInstance (typeof (T), constructorSignature, arguments);
				JNIEnv.FinishCreateInstance (instance, constructorSignature, arguments);
			} finally {
				if (instance != IntPtr.Zero) {
					JNIEnv.DeleteLocalRef (instance);
				}
			}
		}

		static void AssertRegisteredSame<T> (T instance)
			where T : Java.Lang.Object
		{
			var registered = Java.Lang.Object.GetObject<T> (instance.Handle, JniHandleOwnership.DoNotTransfer);
			try {
				Assert.AreSame (instance, registered);
				if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
					Assert.AreEqual (Java.Lang.JavaSystem.IdentityHashCode (instance), instance.JniIdentityHashCode);
				}
			} finally {
				if (registered != null && !object.ReferenceEquals (instance, registered))
					registered.Dispose ();
			}
		}

		static void AssertSameJavaObject (Java.Lang.Object expected, Java.Lang.Object actual)
		{
			Assert.IsNotNull (expected);
			Assert.IsNotNull (actual);
			Assert.IsTrue (
				JniEnvironment.Types.IsSameObject (expected.PeerReference, actual.PeerReference),
				$"Expected Java object identity to match. Expected handle: {expected.Handle}, actual handle: {actual.Handle}.");
		}
	}

	[Register ("net/dot/android/test/ConstructorActivationThrowing")]
	public class ConstructorActivationThrowing : Java.Lang.Object
	{
		public const string ExceptionMessage = "constructor activation throw";

		public static int ConstructorInvocations;

		public ConstructorActivationThrowing ()
		{
			ConstructorInvocations++;
			throw new InvalidOperationException (ExceptionMessage);
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
		}
	}

	[Register ("net/dot/android/test/ConstructorActivationBase", DoNotGenerateAcw = true)]
	public class ConstructorActivationBase : Java.Lang.Object
	{
		public ConstructorActivationBase ()
		{
		}

		public ConstructorActivationBase (bool value)
		{
		}

		public ConstructorActivationBase (sbyte value)
		{
		}

		public ConstructorActivationBase (char value)
		{
		}

		public ConstructorActivationBase (short value)
		{
		}

		public ConstructorActivationBase (int value)
		{
		}

		public ConstructorActivationBase (long value)
		{
		}

		public ConstructorActivationBase (float value)
		{
		}

		public ConstructorActivationBase (double value)
		{
		}

		public ConstructorActivationBase (int number, bool flag, long longValue)
		{
		}

		public ConstructorActivationBase (string value)
		{
		}

		public ConstructorActivationBase (string first, string second)
		{
		}

		public ConstructorActivationBase (string text, int number)
		{
		}

		public ConstructorActivationBase (int[] value)
		{
		}

		public ConstructorActivationBase (string label, int[] value)
		{
		}

		public ConstructorActivationBase (bool[] value)
		{
		}

		public ConstructorActivationBase (byte[] value)
		{
		}

		public ConstructorActivationBase (string[] value)
		{
		}

		public ConstructorActivationBase (Java.Lang.Object[] value)
		{
		}

		public ConstructorActivationBase (int[][] value)
		{
		}

		public ConstructorActivationBase (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	[Register ("net/dot/android/test/ConstructorActivationDefault")]
	public class ConstructorActivationDefault : Java.Lang.Object
	{
		public static int ConstructorInvocations;

		public int ConstructorOrdinal;

		public ConstructorActivationDefault ()
		{
			ConstructorOrdinal = ++ConstructorInvocations;
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
		}
	}

	[Register ("net/dot/android/test/ConstructorActivationMarshalObject")]
	public class ConstructorActivationMarshalObject : ConstructorActivationBase
	{
		public static int ConstructorInvocations;

		public bool BooleanValue;
		public sbyte ByteValue;
		public char CharValue;
		public short ShortValue;
		public int IntValue;
		public long LongValue;
		public float FloatValue;
		public double DoubleValue;
		public int MultiIntValue;
		public bool MultiBooleanValue;
		public long MultiLongValue;
		public string StringValue;
		public string FirstStringValue;
		public string SecondStringValue;
		public string StringIntStringValue;
		public int StringIntValue;
		public int[] IntArrayValue;
		public string StringArrayLabel;
		public int[] StringArrayValues;
		public bool[] BooleanArrayValue;
		public byte[] ByteArrayValue;
		public string[] StringArrayValue;
		public Java.Lang.Object[] ObjectArrayValue;
		public int[][] NestedIntArrayValue;

		[Register (".ctor", "(Z)V", "")]
		public ConstructorActivationMarshalObject (bool value)
			: base (value)
		{
			ConstructorInvocations++;
			BooleanValue = value;
		}

		[Register (".ctor", "(B)V", "")]
		public ConstructorActivationMarshalObject (sbyte value)
			: base (value)
		{
			ConstructorInvocations++;
			ByteValue = value;
		}

		[Register (".ctor", "(C)V", "")]
		public ConstructorActivationMarshalObject (char value)
			: base (value)
		{
			ConstructorInvocations++;
			CharValue = value;
		}

		[Register (".ctor", "(S)V", "")]
		public ConstructorActivationMarshalObject (short value)
			: base (value)
		{
			ConstructorInvocations++;
			ShortValue = value;
		}

		[Register (".ctor", "(I)V", "")]
		public ConstructorActivationMarshalObject (int value)
			: base (value)
		{
			ConstructorInvocations++;
			IntValue = value;
		}

		[Register (".ctor", "(J)V", "")]
		public ConstructorActivationMarshalObject (long value)
			: base (value)
		{
			ConstructorInvocations++;
			LongValue = value;
		}

		[Register (".ctor", "(F)V", "")]
		public ConstructorActivationMarshalObject (float value)
			: base (value)
		{
			ConstructorInvocations++;
			FloatValue = value;
		}

		[Register (".ctor", "(D)V", "")]
		public ConstructorActivationMarshalObject (double value)
			: base (value)
		{
			ConstructorInvocations++;
			DoubleValue = value;
		}

		[Register (".ctor", "(IZJ)V", "")]
		public ConstructorActivationMarshalObject (int number, bool flag, long longValue)
			: base (number, flag, longValue)
		{
			ConstructorInvocations++;
			MultiIntValue = number;
			MultiBooleanValue = flag;
			MultiLongValue = longValue;
		}

		[Register (".ctor", "(Ljava/lang/String;)V", "")]
		public ConstructorActivationMarshalObject (string value)
			: base (value)
		{
			ConstructorInvocations++;
			StringValue = value;
		}

		[Register (".ctor", "(Ljava/lang/String;Ljava/lang/String;)V", "")]
		public ConstructorActivationMarshalObject (string first, string second)
			: base (first, second)
		{
			ConstructorInvocations++;
			FirstStringValue = first;
			SecondStringValue = second;
		}

		[Register (".ctor", "(Ljava/lang/String;I)V", "")]
		public ConstructorActivationMarshalObject (string text, int number)
			: base (text, number)
		{
			ConstructorInvocations++;
			StringIntStringValue = text;
			StringIntValue = number;
		}

		[Register (".ctor", "([I)V", "")]
		public ConstructorActivationMarshalObject (int[] value)
			: base (value)
		{
			ConstructorInvocations++;
			IntArrayValue = value;
		}

		[Register (".ctor", "(Ljava/lang/String;[I)V", "")]
		public ConstructorActivationMarshalObject (string label, int[] value)
			: base (label, value)
		{
			ConstructorInvocations++;
			StringArrayLabel = label;
			StringArrayValues = value;
		}

		[Register (".ctor", "([Z)V", "")]
		public ConstructorActivationMarshalObject (bool[] value)
			: base (value)
		{
			ConstructorInvocations++;
			BooleanArrayValue = value;
		}

		[Register (".ctor", "([B)V", "")]
		public ConstructorActivationMarshalObject (byte[] value)
			: base (value)
		{
			ConstructorInvocations++;
			ByteArrayValue = value;
		}

		[Register (".ctor", "([Ljava/lang/String;)V", "")]
		public ConstructorActivationMarshalObject (string[] value)
			: base (value)
		{
			ConstructorInvocations++;
			StringArrayValue = value;
		}

		[Register (".ctor", "([Ljava/lang/Object;)V", "")]
		public ConstructorActivationMarshalObject (Java.Lang.Object[] value)
			: base (value)
		{
			ConstructorInvocations++;
			ObjectArrayValue = value;
		}

		[Register (".ctor", "([[I)V", "")]
		public ConstructorActivationMarshalObject (int[][] value)
			: base (value)
		{
			ConstructorInvocations++;
			NestedIntArrayValue = value;
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
		}
	}

	[Register ("net/dot/android/test/ConstructorActivationContextView")]
	public class ConstructorActivationContextView : View
	{
		public static int ConstructorInvocations;

		public Context ContextValue;

		[Register (".ctor", "(Landroid/content/Context;)V", "")]
		public ConstructorActivationContextView (Context context)
			: base (context)
		{
			ConstructorInvocations++;
			ContextValue = context;
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
		}
	}

	[Register ("net/dot/android/test/ConstructorActivationAttributeSetView")]
	public class ConstructorActivationAttributeSetView : View
	{
		public static int ConstructorInvocations;

		public Context ContextValue;
		public IAttributeSet AttributeSetValue;

		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;)V", "")]
		public ConstructorActivationAttributeSetView (Context context, IAttributeSet attrs)
			: base (context, attrs)
		{
			ConstructorInvocations++;
			ContextValue = context;
			AttributeSetValue = attrs;
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
		}
	}

	[Register ("net/dot/android/test/ConstructorActivationStyledView")]
	public class ConstructorActivationStyledView : View
	{
		public static int ConstructorInvocations;

		public Context ContextValue;
		public IAttributeSet AttributeSetValue;
		public int DefStyleAttrValue;

		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", "")]
		public ConstructorActivationStyledView (Context context, IAttributeSet attrs, int defStyleAttr)
			: base (context, attrs, defStyleAttr)
		{
			ConstructorInvocations++;
			ContextValue = context;
			AttributeSetValue = attrs;
			DefStyleAttrValue = defStyleAttr;
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
		}
	}
}
