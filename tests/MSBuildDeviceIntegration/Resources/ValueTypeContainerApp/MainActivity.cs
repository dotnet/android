using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;

using Java.Interop;

namespace ${ROOT_NAMESPACE}
{
	[Register ("${JAVA_PACKAGENAME}.MainActivity"), Activity (Label = "${PROJECT_NAME}", MainLauncher = true)]
	public class MainActivity : Activity
	{
		const string ResultPrefix = "VALUE_TYPE_CONTAINER_RESULT";
		const string Tag = "ValueTypeContainers";

		public MainActivity ()
		{
		}

		public MainActivity (IntPtr handle, JniHandleOwnership ownership)
			: base (handle, ownership)
		{
		}

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			int passed = 0;
			try {
				JavaList_NullablePrimitivePreservesNullAndDefault ();
				passed++;
				JavaList_UserStructSupportsOperationsAndRoundTrip ();
				passed++;
				JavaCollection_NonIntEnumSupportsOperationsAndRoundTrip ();
				passed++;
				JavaList_NullableEnumPreservesNullAndDefault ();
				passed++;
				JavaObjectArray_NullableUserStructPreservesNullAndDefault ();
				passed++;
				JavaDictionary_UserStructKeysSupportOperationsAndRoundTrip ();
				passed++;
				JavaDictionary_EnumValuesSupportOperationsAndRoundTrip ();
				passed++;
				JavaDictionary_UserStructKeysAndEnumValuesSupportOperationsAndRoundTrip ();
				passed++;
				JavaDictionary_NullableValueTypesPreserveNullAndDefault ();
				passed++;
				Log.Info (Tag, $"{ResultPrefix} PASS {passed}/9");
			} catch (Exception e) {
				Log.Error (Tag, $"{ResultPrefix} FAIL {passed}/9: {e}");
			} finally {
				Finish ();
			}
		}

		static void JavaList_NullablePrimitivePreservesNullAndDefault ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var list = holder.CreateNullableIntList ();
			try {
				AssertWrapperType (list, typeof (JavaList<>), typeof (int?));
				list.Add (17);
				list.Add (0);
				list.Add (null);
				AssertEqual (3, list.Count, "nullable primitive list count");
				AssertEqual ((int?) 17, list [0], "nullable primitive value");
				AssertEqual ((int?) 0, list [1], "nullable primitive default");
				AssertNull (list [2], "nullable primitive null");
				AssertTrue (list.Contains (null), "nullable primitive list contains null");
				AssertTrue (list.Remove (0), "nullable primitive list removes default");

				var roundTrip = holder.RoundTripNullableIntList (list);
				try {
					AssertWrapperType (roundTrip, typeof (JavaList<>), typeof (int?));
					AssertSameJavaObject (list, roundTrip);
					AssertEqual ((int?) 17, roundTrip [0], "round-tripped nullable primitive");
				} finally {
					DisposeIfDistinct (list, roundTrip);
				}
			} finally {
				DisposeJavaObject (list);
			}
		}

		static void JavaList_UserStructSupportsOperationsAndRoundTrip ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var list = holder.CreateStructList ();
			try {
				AssertWrapperType (list, typeof (JavaList<>), typeof (AppValue));
				var first = new AppValue (11, 111);
				var second = new AppValue (22, 222);
				list.Add (first);
				list.Add (default);
				list.Add (second);
				AssertEqual (3, list.Count, "struct list count");
				AssertEqual (first, list [0], "struct list first");
				AssertEqual (default (AppValue), list [1], "struct list default");
				AssertSequence ([first, default, second], list, "struct list enumeration");

				var roundTrip = holder.RoundTripStructList (list);
				try {
					AssertWrapperType (roundTrip, typeof (JavaList<>), typeof (AppValue));
					AssertSameJavaObject (list, roundTrip);
					AssertEqual (second, roundTrip [2], "round-tripped struct");
				} finally {
					DisposeIfDistinct (list, roundTrip);
				}

				// Value proxies are distinct Java objects in the llvm-ir contract, so equality-based
				// Contains/Remove cannot find a separately boxed struct. Index-based removal is supported.
				list.RemoveAt (0);
				AssertSequence ([default, second], list, "struct list after indexed remove");
			} finally {
				DisposeJavaObject (list);
			}
		}

		static void JavaCollection_NonIntEnumSupportsOperationsAndRoundTrip ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var collection = holder.CreateEnumCollection ();
			try {
				AssertWrapperType (collection, typeof (JavaCollection<>), typeof (AppState));
				collection.Add (AppState.Ready);
				collection.Add (default);
				AssertEqual (2, collection.Count, "enum collection count");
				AssertSequence ([AppState.Ready, default], collection, "enum collection enumeration");

				var roundTrip = holder.RoundTripEnumCollection (collection);
				try {
					AssertWrapperType (roundTrip, typeof (JavaCollection<>), typeof (AppState));
					AssertSameJavaObject (collection, roundTrip);
					AssertSequence ([AppState.Ready, default], roundTrip, "round-tripped enum collection");
				} finally {
					DisposeIfDistinct (collection, roundTrip);
				}

				collection.Clear ();
				AssertEqual (0, collection.Count, "enum collection count after clear");
			} finally {
				DisposeJavaObject (collection);
			}
		}

		static void JavaList_NullableEnumPreservesNullAndDefault ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var list = holder.CreateNullableEnumList ();
			try {
				AssertWrapperType (list, typeof (JavaList<>), typeof (AppState?));
				list.Add (AppState.Busy);
				list.Add (default (AppState));
				list.Add (null);
				AssertEqual ((AppState?) AppState.Busy, list [0], "nullable enum value");
				AssertEqual ((AppState?) AppState.None, list [1], "nullable enum default");
				AssertNull (list [2], "nullable enum null");
				AssertTrue (list.Contains (null), "nullable enum list contains null");
				AssertSequence ([AppState.Busy, AppState.None, null], list, "nullable enum enumeration");
			} finally {
				DisposeJavaObject (list);
			}
		}

		static void JavaObjectArray_NullableUserStructPreservesNullAndDefault ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			using var array = holder.CreateNullableStructArray (3);
			var value = new AppValue (31, 331);
			array [0] = value;
			array [1] = default (AppValue);
			array [2] = null;

			AssertWrapperType (array, typeof (JavaObjectArray<>), typeof (AppValue?));
			AssertEqual ((AppValue?) value, array [0], "nullable struct array value");
			AssertEqual ((AppValue?) default (AppValue), array [1], "nullable struct array default");
			AssertNull (array [2], "nullable struct array null");
			AssertSequence ([value, (AppValue?) default (AppValue), null], array, "nullable struct array enumeration");

			using var roundTrip = holder.RoundTripNullableStructArray (array);
			AssertWrapperType (roundTrip, typeof (JavaObjectArray<>), typeof (AppValue?));
			AssertSameJavaObject (array, roundTrip);
			AssertEqual ((AppValue?) value, roundTrip [0], "round-tripped nullable struct array");
		}

		static void JavaDictionary_UserStructKeysSupportOperationsAndRoundTrip ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var dictionary = holder.CreateStructKeyDictionary ();
			try {
				AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (AppValue), typeof (string));
				var first = new AppValue (41, 441);
				dictionary.Add (first, "first");
				dictionary.Add (default, "default");
				AssertEqual (2, dictionary.Count, "struct-key dictionary count");
				AssertFalse (dictionary.ContainsKey (first), "struct-key lookup uses Java proxy identity");
				AssertThrows<KeyNotFoundException> (() => _ = dictionary [first], "struct-key indexer");

				var roundTrip = holder.RoundTripStructKeyDictionary (dictionary);
				try {
					AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (AppValue), typeof (string));
					AssertSameJavaObject (dictionary, roundTrip);
					AssertEqual (2, roundTrip.Count, "round-tripped struct-key dictionary count");
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				// The llvm-ir value proxy uses Java identity for separately boxed struct keys. Key
				// lookup, removal, and enumeration are therefore outside the parity contract.
				dictionary.Clear ();
				AssertEqual (0, dictionary.Count, "struct-key dictionary count after clear");
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static void JavaDictionary_EnumValuesSupportOperationsAndRoundTrip ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var dictionary = holder.CreateEnumValueDictionary ();
			try {
				AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (string), typeof (AppState));
				dictionary.Add ("ready", AppState.Ready);
				dictionary.Add ("default", default);
				AssertEqual (AppState.Ready, dictionary ["ready"], "enum-value dictionary ready");
				AssertEqual (AppState.None, dictionary ["default"], "enum-value dictionary default");
				AssertTrue (ContainsValue (dictionary, AppState.Ready), "enum-value dictionary enumeration");

				var roundTrip = holder.RoundTripEnumValueDictionary (dictionary);
				try {
					AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (string), typeof (AppState));
					AssertSameJavaObject (dictionary, roundTrip);
					AssertEqual (AppState.Ready, roundTrip ["ready"], "round-tripped enum value");
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				AssertTrue (dictionary.Remove ("ready"), "enum-value dictionary removes ready");
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static void JavaDictionary_UserStructKeysAndEnumValuesSupportOperationsAndRoundTrip ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var dictionary = holder.CreateStructEnumDictionary ();
			try {
				AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (AppValue), typeof (AppState));
				var key = new AppValue (51, 551);
				dictionary.Add (key, AppState.Busy);
				AssertFalse (dictionary.ContainsKey (key), "value/value lookup uses Java proxy identity");
				AssertThrows<KeyNotFoundException> (() => _ = dictionary [key], "value/value indexer");

				var roundTrip = holder.RoundTripStructEnumDictionary (dictionary);
				try {
					AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (AppValue), typeof (AppState));
					AssertSameJavaObject (dictionary, roundTrip);
					AssertEqual (1, roundTrip.Count, "round-tripped value/value dictionary count");
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static void JavaDictionary_NullableValueTypesPreserveNullAndDefault ()
		{
			using var holder = new RawValueTypeContainerHolder ();
			var dictionary = holder.CreateNullableDictionary ();
			try {
				AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (AppValue?), typeof (AppState?));
				var key = new AppValue (61, 661);
				dictionary.Add (key, AppState.Ready);
				dictionary.Add (default (AppValue), default (AppState));
				dictionary.Add (null, null);
				AssertFalse (dictionary.ContainsKey (key), "nullable non-null key uses Java proxy identity");
				AssertNull (dictionary [null], "nullable dictionary null");
				AssertTrue (dictionary.ContainsKey (null), "nullable dictionary contains null key");
				AssertTrue (dictionary.Remove (null), "nullable dictionary removes null key");
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static bool ContainsValue<TKey, TValue> (IDictionary<TKey, TValue> dictionary, TValue expected)
		{
			foreach (var pair in dictionary) {
				if (EqualityComparer<TValue>.Default.Equals (expected, pair.Value)) {
					return true;
				}
			}
			return false;
		}

		static void AssertSequence<T> (T [] expected, IEnumerable<T> actual, string message)
		{
			int index = 0;
			foreach (var item in actual) {
				if (index >= expected.Length) {
					throw new InvalidOperationException ($"Assertion failed: {message}; too many elements.");
				}
				AssertEqual (expected [index], item, $"{message} element {index}");
				index++;
			}
			AssertEqual (expected.Length, index, $"{message} length");
		}

		static void AssertWrapperType (object wrapper, Type expectedGenericDefinition, params Type [] expectedArguments)
		{
			var wrapperType = wrapper.GetType ();
			AssertTrue (wrapperType.IsGenericType, "wrapper must be generic");
			AssertEqual (expectedGenericDefinition, wrapperType.GetGenericTypeDefinition (), "wrapper generic definition");
			AssertEqual (expectedArguments.Length, wrapperType.GenericTypeArguments.Length, "wrapper generic argument count");
			for (int i = 0; i < expectedArguments.Length; i++) {
				AssertEqual (expectedArguments [i], wrapperType.GenericTypeArguments [i], $"wrapper generic argument {i}");
			}
		}

		[DynamicDependency ("FromJniHandle", "Java.Interop.JavaConvert", "Mono.Android")]
		static object InvokeJavaConvertFromJniHandle (Type targetType, IntPtr handle)
		{
			var javaConvert = typeof (Java.Lang.Object).Assembly.GetType ("Java.Interop.JavaConvert");
			if (javaConvert == null) {
				throw new InvalidOperationException ("JavaConvert type was not found.");
			}

			var method = javaConvert.GetMethod (
				"FromJniHandle",
				BindingFlags.Public | BindingFlags.Static,
				binder: null,
				types: [typeof (IntPtr), typeof (JniHandleOwnership), typeof (Type)],
				modifiers: null);
			if (method == null) {
				throw new InvalidOperationException ("JavaConvert.FromJniHandle method was not found.");
			}

			var value = method.Invoke (null, [handle, JniHandleOwnership.TransferLocalRef, targetType]);
			if (value == null) {
				throw new InvalidOperationException ($"JavaConvert returned null for target type '{targetType}'.");
			}
			return value;
		}

		static void AssertSameJavaObject (object expected, object actual)
		{
			var expectedPeer = (IJavaPeerable) expected;
			var actualPeer = (IJavaPeerable) actual;
			AssertTrue (
				JNIEnv.IsSameObject (expectedPeer.PeerReference.Handle, actualPeer.PeerReference.Handle),
				"expected identical Java peers");
		}

		static void AssertTrue (bool value, string message)
		{
			if (!value) {
				throw new InvalidOperationException ($"Assertion failed: {message}.");
			}
		}

		static void AssertFalse (bool value, string message)
		{
			AssertTrue (!value, message);
		}

		static void AssertThrows<TException> (Action action, string message)
			where TException : Exception
		{
			try {
				action ();
			} catch (TException) {
				return;
			}
			throw new InvalidOperationException ($"Assertion failed: {message}; expected {typeof (TException)}.");
		}

		static void AssertNull<T> (T? value, string message)
			where T : struct
		{
			if (value.HasValue) {
				throw new InvalidOperationException ($"Assertion failed: {message}; expected null, found '{value}'.");
			}
		}

		static void AssertEqual<T> (T expected, T actual, string message)
		{
			if (!EqualityComparer<T>.Default.Equals (expected, actual)) {
				throw new InvalidOperationException ($"Assertion failed: {message}; expected '{expected}', found '{actual}'.");
			}
		}

		static void DisposeIfDistinct (object owner, object value)
		{
			if (!ReferenceEquals (owner, value)) {
				DisposeJavaObject (value);
			}
		}

		static void DisposeJavaObject (object value)
		{
			if (value is IDisposable disposable) {
				disposable.Dispose ();
			}
		}

		readonly struct AppValue : IEquatable<AppValue>
		{
			public AppValue (int first, int second)
			{
				First = first;
				Second = second;
			}

			public int First { get; }
			public int Second { get; }

			public bool Equals (AppValue other) => First == other.First && Second == other.Second;

			public override bool Equals (object value) => value is AppValue other && Equals (other);

			public override int GetHashCode () => HashCode.Combine (First, Second);

			public override string ToString () => $"{First}:{Second}";
		}

		enum AppState : short
		{
			None = 0,
			Ready = 7,
			Busy = 300,
		}

		sealed class RawValueTypeContainerHolder : IDisposable
		{
			const string ArraySignature = "()[Ljava/lang/Object;";
			const string CollectionSignature = "()Ljava/util/Collection;";
			const string DictionarySignature = "()Ljava/util/Map;";
			const string JniName = "net/dot/android/test/ValueTypeContainerFixture";
			const string ListSignature = "()Ljava/util/List;";
			const string RoundTripArraySignature = "([Ljava/lang/Object;)[Ljava/lang/Object;";
			const string RoundTripCollectionSignature = "(Ljava/util/Collection;)Ljava/util/Collection;";
			const string RoundTripDictionarySignature = "(Ljava/util/Map;)Ljava/util/Map;";
			const string RoundTripListSignature = "(Ljava/util/List;)Ljava/util/List;";

			readonly Java.Lang.Object holder;

			public RawValueTypeContainerHolder ()
			{
				var holderClass = JniEnvironment.Types.FindClass (JniName);
				try {
					var constructor = JNIEnv.GetMethodID (holderClass.Handle, "<init>", "()V");
					var handle = JNIEnv.NewObject (holderClass.Handle, constructor);
					holder = new Java.Lang.Object (handle, JniHandleOwnership.TransferLocalRef);
				} finally {
					JniObjectReference.Dispose (ref holderClass);
				}
			}

			public IList<int?> CreateNullableIntList () => ConvertContainer<IList<int?>> (Call ("createList", ListSignature));

			public IList<int?> RoundTripNullableIntList (IList<int?> value) =>
				ConvertContainer<IList<int?>> (Call ("roundTripList", RoundTripListSignature, value));

			public IList<AppValue> CreateStructList () => ConvertContainer<IList<AppValue>> (Call ("createList", ListSignature));

			public IList<AppValue> RoundTripStructList (IList<AppValue> value) =>
				ConvertContainer<IList<AppValue>> (Call ("roundTripList", RoundTripListSignature, value));

			public ICollection<AppState> CreateEnumCollection () =>
				ConvertContainer<ICollection<AppState>> (Call ("createCollection", CollectionSignature));

			public ICollection<AppState> RoundTripEnumCollection (ICollection<AppState> value) =>
				ConvertContainer<ICollection<AppState>> (Call ("roundTripCollection", RoundTripCollectionSignature, value));

			public IList<AppState?> CreateNullableEnumList () =>
				ConvertContainer<IList<AppState?>> (Call ("createList", ListSignature));

			public JavaObjectArray<AppValue?> CreateNullableStructArray (int length) =>
				WrapNullableStructArray (Call ("createArray", $"(I){ArraySignature.Substring (2)}", length));

			public JavaObjectArray<AppValue?> RoundTripNullableStructArray (JavaObjectArray<AppValue?> value) =>
				WrapNullableStructArray (Call ("roundTripArray", RoundTripArraySignature, value));

			public IDictionary<AppValue, string> CreateStructKeyDictionary () =>
				ConvertContainer<IDictionary<AppValue, string>> (Call ("createDictionary", DictionarySignature));

			public IDictionary<AppValue, string> RoundTripStructKeyDictionary (IDictionary<AppValue, string> value) =>
				ConvertContainer<IDictionary<AppValue, string>> (Call ("roundTripDictionary", RoundTripDictionarySignature, value));

			public IDictionary<string, AppState> CreateEnumValueDictionary () =>
				ConvertContainer<IDictionary<string, AppState>> (Call ("createDictionary", DictionarySignature));

			public IDictionary<string, AppState> RoundTripEnumValueDictionary (IDictionary<string, AppState> value) =>
				ConvertContainer<IDictionary<string, AppState>> (Call ("roundTripDictionary", RoundTripDictionarySignature, value));

			public IDictionary<AppValue, AppState> CreateStructEnumDictionary () =>
				ConvertContainer<IDictionary<AppValue, AppState>> (Call ("createDictionary", DictionarySignature));

			public IDictionary<AppValue, AppState> RoundTripStructEnumDictionary (IDictionary<AppValue, AppState> value) =>
				ConvertContainer<IDictionary<AppValue, AppState>> (Call ("roundTripDictionary", RoundTripDictionarySignature, value));

			public IDictionary<AppValue?, AppState?> CreateNullableDictionary () =>
				ConvertContainer<IDictionary<AppValue?, AppState?>> (Call ("createDictionary", DictionarySignature));

			public void Dispose ()
			{
				holder.Dispose ();
			}

			static T ConvertContainer<T> (IntPtr handle) => (T) InvokeJavaConvertFromJniHandle (typeof (T), handle);

			static JavaObjectArray<AppValue?> WrapNullableStructArray (IntPtr handle)
			{
				var reference = new JniObjectReference (handle, JniObjectReferenceType.Local);
				return new JavaObjectArray<AppValue?> (ref reference, JniObjectReferenceOptions.CopyAndDispose);
			}

			IntPtr Call (string methodName, string signature, object value = null)
			{
				var holderClass = JniEnvironment.Types.GetObjectClass (holder.PeerReference);
				try {
					var method = JNIEnv.GetMethodID (holderClass.Handle, methodName, signature);
					if (value == null) {
						return JNIEnv.CallObjectMethod (holder.Handle, method);
					}
					var peer = (IJavaPeerable) value;
					var result = JNIEnv.CallObjectMethod (holder.Handle, method, new JValue (peer.PeerReference.Handle));
					GC.KeepAlive (value);
					return result;
				} finally {
					JniObjectReference.Dispose (ref holderClass);
				}
			}

			IntPtr Call (string methodName, string signature, int value)
			{
				var holderClass = JniEnvironment.Types.GetObjectClass (holder.PeerReference);
				try {
					var method = JNIEnv.GetMethodID (holderClass.Handle, methodName, signature);
					return JNIEnv.CallObjectMethod (holder.Handle, method, new JValue (value));
				} finally {
					JniObjectReference.Dispose (ref holderClass);
				}
			}
		}
	}
}
