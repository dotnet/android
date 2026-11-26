#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JniTypeUtf8Test {

		const string JniReferenceLeakCategory = "JniReferenceLeak";
		internal const string UnicodeTypeName = "net/dot/jni/test/UnicodeNames";
		internal const string UnicodeNativeName = "native\U00010000";
		internal const string UnicodeNativeSignature = "(Lnet/dot/jni/test/UnicodeNames$Value\U00010000;)I";
		public TestContext? TestContext { get; set; }

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void ModifiedUtf8_EncodesUtf16CodeUnits ()
		{
			const string value = "A\0\u007f\u0080\u07ff\u0800\ud800\udc00\udbff\udfff\udfff";
			byte [] expected = [
				0x41, 0xc0, 0x80, 0x7f, 0xc2, 0x80, 0xdf, 0xbf, 0xe0, 0xa0, 0x80,
				0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80, 0xed, 0xaf, 0xbf, 0xed, 0xbf, 0xbf,
				0xed, 0xbf, 0xbf,
			];
			var actual = new byte [JniModifiedUtf8.GetByteCount (value)];
			JniModifiedUtf8.GetBytes (value, actual);
			CollectionAssert.AreEqual (expected, actual);
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void ModifiedUtf8_ConvertsSupplementaryUtf8 ()
		{
			byte [] source = [
				0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80,
				0xf0, 0x90, 0x80, 0x80, 0xf4, 0x8f, 0xbf, 0xbf, 0,
			];
			byte [] expected = [
				0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80,
				0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80,
				0xed, 0xaf, 0xbf, 0xed, 0xbf, 0xbf, 0,
			];
			unsafe {
				// SAFETY: source remains pinned and NUL-terminated through conversion.
				fixed (byte* bytes = source) {
					var converted = JniModifiedUtf8.AllocConvertedUtf8 (bytes, "value");
					try {
						Assert.AreNotEqual (IntPtr.Zero, converted);
						var actual = new byte [expected.Length];
						Marshal.Copy (converted, actual, 0, actual.Length);
						CollectionAssert.AreEqual (expected, actual);
					} finally {
						JniModifiedUtf8.Free (converted);
					}
				}
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void ModifiedUtf8_HandlesVectorBoundaries ()
		{
			const string pattern = "A\0\u007f\u0080\u07ff\u0800\ud800\udc00\udfff";
			byte [][] encoded = [
				[0x41], [0xc0, 0x80], [0x7f], [0xc2, 0x80], [0xdf, 0xbf],
				[0xe0, 0xa0, 0x80], [0xed, 0xa0, 0x80], [0xed, 0xb0, 0x80], [0xed, 0xbf, 0xbf],
			];
			for (int start = 0; start < 16; start++) {
				for (int length = 0; length <= 65; length++) {
					var chars = new char [start + length + 1];
					Array.Fill (chars, '\uffff');
					var expected = new List<byte> ();
					for (int i = 0; i < length; i++) {
						int index = (start + i) % pattern.Length;
						chars [start + i] = pattern [index];
						expected.AddRange (encoded [index]);
					}
					var value = chars.AsSpan (start, length);
					Assert.AreEqual (expected.Count, JniModifiedUtf8.GetByteCount (value), $"start={start}, length={length}");
					var destination = new byte [expected.Count + 2];
					Array.Fill (destination, (byte) 0xcc);
					JniModifiedUtf8.GetBytes (value, destination.AsSpan (1, expected.Count));
					CollectionAssert.AreEqual (expected.ToArray (), destination.AsSpan (1, expected.Count).ToArray ());
					Assert.AreEqual ((byte) 0xcc, destination [0]);
					Assert.AreEqual ((byte) 0xcc, destination [destination.Length - 1]);
				}
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void ModifiedUtf8_PreservesStructurallyValidNonScalarSequences ()
		{
			// ART CheckJNI accepts these lead/continuation shapes; only valid four-byte scalars are rewritten.
			byte [][] unchanged = [
				[0xf0, 0x8f, 0xbf, 0xbf], [0xf4, 0x90, 0x80, 0x80], [0xf5, 0x80, 0x80, 0x80],
				[0xf7, 0xbf, 0xbf, 0xbf], [0xc0, 0x80], [0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80],
			];
			for (int prefix = 0; prefix <= 33; prefix++) {
				foreach (var bytes in unchanged) {
					var source = new byte [prefix + bytes.Length + 5];
					Array.Fill (source, (byte) 'a', 0, prefix);
					bytes.CopyTo (source, prefix);
					// The scalar beyond this terminator must not be inspected.
					source [prefix + bytes.Length + 1] = 0xf0;
					source [prefix + bytes.Length + 2] = 0x90;
					source [prefix + bytes.Length + 3] = 0x80;
					source [prefix + bytes.Length + 4] = 0x80;
					unsafe {
						// SAFETY: source stays pinned and NUL-terminated through each conversion.
						fixed (byte* pointer = source) {
							Assert.IsFalse (JniModifiedUtf8.RequiresConversion (pointer, "value"));
							Assert.AreEqual (IntPtr.Zero, JniModifiedUtf8.AllocConvertedUtf8 (pointer, "value"));
							source [prefix + bytes.Length] = 0xf0;
							source [prefix + bytes.Length + 1] = 0x90;
							source [prefix + bytes.Length + 2] = 0x80;
							source [prefix + bytes.Length + 3] = 0x80;
							source [source.Length - 1] = 0;
							Assert.IsTrue (JniModifiedUtf8.RequiresConversion (pointer, "value"));
							IntPtr converted = JniModifiedUtf8.AllocConvertedUtf8 (pointer, "value");
							try {
								Assert.AreNotEqual (IntPtr.Zero, converted);
								var expected = new List<byte> (source.AsSpan (0, prefix + bytes.Length).ToArray ());
								expected.AddRange ([0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80, 0]);
								var actual = new byte [expected.Count];
								Marshal.Copy (converted, actual, 0, actual.Length);
								CollectionAssert.AreEqual (expected.ToArray (), actual);
							} finally {
								JniModifiedUtf8.Free (converted);
							}
						}
					}
				}
			}
		}

		internal static readonly byte [][] MalformedUtf8 = [
			[0xf0], [0xf0, 0x90], [0xf0, 0x90, 0x80], [0xe0, 0xa0], [0xc2],
			[0xf0, 0xff, 0x80, 0x80], [0xf0, 0x90, 0x7f, 0x80], [0xf0, 0x90, 0x80, 0xc0],
			[0x80], [0xbf], [0xf8, 0x80, 0x80, 0x80, 0x80], [0xff], [0xc2, 0x41],
		];

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void ModifiedUtf8_RejectsMalformedAndTruncatedUtf8 ()
		{
			for (int prefix = 0; prefix <= 33; prefix++) {
				foreach (var bytes in MalformedUtf8) {
					// Truncated sequences end at the NUL; valid bytes beyond it must not complete them.
					var source = new byte [prefix + bytes.Length + 4];
					Array.Fill (source, (byte) 'a', 0, prefix);
					bytes.CopyTo (source, prefix);
					source [prefix + bytes.Length + 1] = 0x80;
					source [prefix + bytes.Length + 2] = 0x80;
					source [prefix + bytes.Length + 3] = 0x80;
					unsafe {
						// SAFETY: source stays pinned and NUL-terminated through each scan.
						fixed (byte* pinned = source) {
							IntPtr pointer = (IntPtr) pinned;
							var e = Assert.ThrowsExactly<ArgumentException> (() => JniModifiedUtf8.Validate ((byte*) pointer, "value"));
							Assert.AreEqual ("value", e.ParamName);
							StringAssert.Contains (e.Message, $"byte offset {prefix}");
							Assert.ThrowsExactly<ArgumentException> (() => JniModifiedUtf8.RequiresConversion ((byte*) pointer, "value"));
							Assert.ThrowsExactly<ArgumentException> (() => JniModifiedUtf8.AllocConvertedUtf8 ((byte*) pointer, "value"));
						}
					}
				}
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void Utf8Apis_RejectMalformedUtf8BeforeJni ()
		{
			byte [] bad = [(byte) 'a', 0xe0, 0xa0, 0];
			using (var Object_class = new JniType ("java/lang/Object")) {
				var r = Object_class.PeerReference;
				// SAFETY: Every buffer is a live, NUL-terminated array or u8 literal; malformed input throws before JNI reads it.
				AssertRejected ("classname", () => { _ = unsafe (JniEnvironment.Types.FindClass (bad)); });
				AssertRejected ("classname", () => { _ = unsafe (JniEnvironment.Types.TryFindClass ([(byte) 'a', (byte) '.', 0xe0, 0xa0, 0], out _)); });
				AssertRejected ("name", () => { _ = unsafe (JniEnvironment.InstanceMethods.GetMethodID (r, bad, "()V"u8)); });
				AssertRejected ("signature", () => { _ = unsafe (JniEnvironment.InstanceMethods.GetMethodID (r, "hashCode"u8, bad)); });
				AssertRejected ("name", () => { _ = unsafe (JniEnvironment.StaticMethods.GetStaticMethodID (r, bad, "()V"u8)); });
				AssertRejected ("name", () => { _ = unsafe (JniEnvironment.InstanceFields.GetFieldID (r, bad, "I"u8)); });
				AssertRejected ("signature", () => { _ = unsafe (JniEnvironment.StaticFields.GetStaticFieldID (r, "x"u8, bad)); });
				AssertRejected ("name", () => { _ = unsafe (Object_class.GetInstanceMethod (bad, "()I"u8)); });
				AssertRejected ("signature", () => { _ = unsafe (Object_class.GetStaticMethod ("x"u8, bad)); });
				AssertRejected ("classname", () => { _ = unsafe (JniEnvironment.Types.TryFindClass ("\0java/lang/Object"u8, out _)); });
				AssertRejected ("name", () => { _ = unsafe (JniEnvironment.InstanceMethods.GetMethodID (r, ""u8, "()V"u8)); });
				AssertRejected ("signature", () => { _ = unsafe (JniEnvironment.InstanceFields.GetFieldID (r, "x"u8, ""u8)); });
				AssertRejected ("name", () => { _ = unsafe (JniEnvironment.InstanceMethods.GetMethodID (r, "\0hashCode"u8, "()I"u8)); });
				AssertRejected ("signature", () => { _ = unsafe (JniEnvironment.StaticMethods.GetStaticMethodID (r, "x"u8, "\0()V"u8)); });
				AssertRejected ("name", () => { _ = unsafe (JniEnvironment.InstanceFields.GetFieldID (r, "\0"u8, "I"u8)); });
				AssertRejected ("signature", () => { _ = unsafe (JniEnvironment.StaticFields.GetStaticFieldID (r, "x"u8, "\0I"u8)); });

				var nullName = default (JniNativeMethodRegistration);
				nullName.Signature = "()V";
				nullName.Marshaler = new Action (() => {});
				var nullSignature = nullName;
				nullSignature.Name = "x";
				nullSignature.Signature = default (JniNativeMethodRegistration).Signature;
				// SAFETY: Null names and signatures are rejected before JNI can retain the callback.
				AssertRejected ("methods", () => { unsafe { Object_class.RegisterNativeMethods (nullName); } });
				AssertRejected ("methods", () => { unsafe { Object_class.RegisterNativeMethods (nullSignature); } });
				unsafe {
					// SAFETY: All buffers stay pinned and terminated; validation throws before JNI sees the callback.
					fixed (byte* good = "native"u8)
					fixed (byte* supplementary = "native\U00010000"u8)
					fixed (byte* badPtr = bad) {
						var callback = (IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>) &UnicodeNativeCallback;
						var methods = new JniNativeMethod [] {
							new JniNativeMethod (good, good, callback),
							new JniNativeMethod (good, badPtr, callback),
						};
						AssertRejected ("methods", () => JniEnvironment.Types.RegisterNatives (r, methods));

						methods [0] = new JniNativeMethod (supplementary, good, callback);
						AssertRejected ("methods", () => JniEnvironment.Types.RegisterNatives (r, methods));

						methods [0] = new JniNativeMethod (supplementary, badPtr, callback);
						AssertRejected ("methods", () => JniEnvironment.Types.RegisterNatives (r, methods));

						methods [0] = default;
						AssertRejected ("methods", () => JniEnvironment.Types.RegisterNatives (r, methods));

						methods [0] = new JniNativeMethod (supplementary, good, callback);
						methods [1] = new JniNativeMethod (good, null, callback);
						AssertRejected ("methods", () => JniEnvironment.Types.RegisterNatives (r, methods));

						IntPtr env = JniEnvironment.EnvironmentPointer;
						IntPtr badName = (IntPtr) badPtr, goodName = (IntPtr) good;
						AssertRejected ("classname", () => JniNativeMethods.FindClass (env, badName));
						AssertRejected ("name", () => JniNativeMethods.GetMethodID (env, r.Handle, badName, goodName));
						AssertRejected ("signature", () => JniNativeMethods.GetStaticFieldID (env, r.Handle, goodName, badName));
					}
				}
				Assert.IsFalse (JniEnvironment.Exceptions.ExceptionOccurred ().IsValid);
			}

			static void AssertRejected (string paramName, Action action)
			{
				var e = Assert.ThrowsExactly<ArgumentException> (action);
				Assert.AreEqual (paramName, e.ParamName);
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void ThrowNew_PreservesNulAndSupplementaryCharacters ()
		{
			const string message = "before\0\U00010000after";
			using (var type = new JniType ("java/lang/IllegalArgumentException")) {
				JniObjectReference thrown = default;
				try {
					JniEnvironment.Exceptions.ThrowNew (type.PeerReference, message);
					thrown = JniEnvironment.Exceptions.ExceptionOccurred ();
				} finally {
					JniEnvironment.Exceptions.ExceptionClear ();
				}
				try {
					Assert.IsTrue (thrown.IsValid);
					var getMessage = type.GetInstanceMethod ("getMessage", "()Ljava/lang/String;");
					// SAFETY: The throwable is live and getMessage takes no arguments.
					var javaMessage = unsafe (JniEnvironment.InstanceMethods.CallObjectMethod (thrown, getMessage));
					try {
						Assert.AreEqual (message, JniEnvironment.Strings.ToString (javaMessage));
					} finally {
						JniObjectReference.Dispose (ref javaMessage);
					}
				} finally {
					JniObjectReference.Dispose (ref thrown);
				}
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void FindClass_SupplementaryName ()
		{
			// ART's String-based Class.forName() cannot load classes whose names contain supplementary
			// characters, so the lookup must use JNI FindClass() with the app class loader. ART selects
			// that loader from the calling native method, so run the lookups inside a UnicodeNames native method.
			using (var type = new JniType (UnicodeTypeName)) {
				try {
					unsafe {
						// SAFETY: Literals remain pinned and terminated through registration; the callback has the matching JNI ABI.
						fixed (byte* name = "native\U00010000"u8)
						fixed (byte* signature = "(Lnet/dot/jni/test/UnicodeNames$Value\U00010000;)I"u8) {
							var methods = new [] {
								new JniNativeMethod (name, signature,
									(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>) &FindSupplementaryClassCallback),
							};
							JniEnvironment.Types.RegisterNatives (type.PeerReference, methods);
						}
					}
					findSupplementaryClassFailure = "callback was not invoked";
					var result = InvokeUnicodeNative (type);
					Assert.AreEqual (1, result, findSupplementaryClassFailure);
				} finally {
					type.UnregisterNativeMethods ();
				}
			}
		}

		[ThreadStatic]
		static string? findSupplementaryClassFailure;

		[UnmanagedCallersOnly]
		static int FindSupplementaryClassCallback (IntPtr env, IntPtr self, IntPtr value)
		{
			try {
				AssertFindClassSupplementaryName ();
				findSupplementaryClassFailure = null;
				return 1;
			} catch (Exception e) {
				findSupplementaryClassFailure = e.ToString ();
				return 0;
			}
		}

		static void AssertFindClassSupplementaryName ()
		{
			const string className = "net/dot/jni/test/UnicodeNames$Value\U00010000";
			byte [] modifiedName = [
				.. "net/dot/jni/test/UnicodeNames$Value"u8,
				0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80, 0,
			];
			using (var fromString = new JniType (className))
			// SAFETY: The literal and modifiedName both have NUL-terminated backing storage.
			using (var fromUtf8 = unsafe (new JniType ("net/dot/jni/test/UnicodeNames$Value\U00010000"u8)))
			using (var fromModifiedUtf8 = unsafe (new JniType (modifiedName))) {
				Assert.AreEqual (className, fromString.Name);
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString.PeerReference, fromUtf8.PeerReference));
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString.PeerReference, fromModifiedUtf8.PeerReference));
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void GetMembers_SupplementaryNamesAndDescriptors ()
		{
			const string instanceName = "method\U00010000";
			const string staticName = "staticMethod\U00010000";
			const string instanceFieldName = "field\U00010000";
			const string staticFieldName = "staticField\U00010000";
			const string valueSignature = "Lnet/dot/jni/test/UnicodeNames$Value\U00010000;";
			const string identitySignature = "(Lnet/dot/jni/test/UnicodeNames$Value\U00010000;)Lnet/dot/jni/test/UnicodeNames$Value\U00010000;";
			byte [] modifiedName = [.. "method"u8, 0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80, 0];
			byte [] modifiedSignature = [
				.. "Lnet/dot/jni/test/UnicodeNames$Value"u8,
				0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80, (byte) ';', 0,
			];

			using (var type = new JniType (UnicodeTypeName)) {
				var instance = type.GetInstanceMethod (instanceName, "()I");
				var staticMethod = type.GetStaticMethod (staticName, "()I");
				var instanceField = type.GetInstanceField (instanceFieldName, "I");
				var staticField = type.GetStaticField (staticFieldName, "I");
				var valueField = type.GetInstanceField ("value", valueSignature);
				var identity = type.GetStaticMethod ("identity", identitySignature);

				Assert.AreEqual (instance.ID, type.GetInstanceMethod (instanceName.AsSpan (), "()I".AsSpan ()).ID);
				Assert.AreEqual (staticMethod.ID, type.GetStaticMethod (staticName.AsSpan (), "()I".AsSpan ()).ID);
				Assert.AreEqual (instanceField.ID, type.GetInstanceField (instanceFieldName.AsSpan (), "I".AsSpan ()).ID);
				Assert.AreEqual (staticField.ID, type.GetStaticField (staticFieldName.AsSpan (), "I".AsSpan ()).ID);
				Assert.IsTrue (type.TryGetInstanceMethod (instanceName, "()I", out var triedInstance));
				Assert.IsTrue (type.TryGetStaticMethod (staticName, "()I", out var triedStatic));
				Assert.AreEqual (instance.ID, triedInstance?.ID);
				Assert.AreEqual (staticMethod.ID, triedStatic?.ID);

				// SAFETY: Each literal or array is terminated and remains alive through its lookup.
				unsafe {
					Assert.AreEqual (instance.ID, type.GetInstanceMethod ("method\U00010000"u8, "()I"u8).ID);
					Assert.AreEqual (instance.ID, type.GetInstanceMethod (modifiedName, "()I"u8).ID);
					Assert.AreEqual (staticMethod.ID, type.GetStaticMethod ("staticMethod\U00010000"u8, "()I"u8).ID);
					Assert.AreEqual (instanceField.ID, type.GetInstanceField ("field\U00010000"u8, "I"u8).ID);
					Assert.AreEqual (staticField.ID, type.GetStaticField ("staticField\U00010000"u8, "I"u8).ID);
					Assert.AreEqual (valueField.ID, type.GetInstanceField ("value"u8, "Lnet/dot/jni/test/UnicodeNames$Value\U00010000;"u8).ID);
					Assert.AreEqual (valueField.ID, type.GetInstanceField ("value"u8, modifiedSignature).ID);
					Assert.AreEqual (identity.ID, type.GetStaticMethod ("identity"u8,
						"(Lnet/dot/jni/test/UnicodeNames$Value\U00010000;)Lnet/dot/jni/test/UnicodeNames$Value\U00010000;"u8).ID);
					fixed (byte* name = "method\U00010000"u8)
					fixed (byte* signature = "()I"u8) {
						Assert.IsTrue (type.TryGetInstanceMethod (name, "()I".AsSpan (), out var fromByteName));
						Assert.IsTrue (type.TryGetInstanceMethod (instanceName.AsSpan (), signature, out var fromByteSignature));
						Assert.IsTrue (type.TryGetInstanceMethod (name, signature, out var fromBytes));
						Assert.AreEqual (instance.ID, fromByteName?.ID);
						Assert.AreEqual (instance.ID, fromByteSignature?.ID);
						Assert.AreEqual (instance.ID, fromBytes?.ID);
					}
					fixed (byte* name = "identity"u8)
					fixed (byte* signature = "(Lnet/dot/jni/test/UnicodeNames$Value\U00010000;)Lnet/dot/jni/test/UnicodeNames$Value\U00010000;"u8) {
						Assert.IsTrue (type.TryGetStaticMethod (name, signature, out var fromBytes));
						Assert.IsTrue (type.TryGetStaticMethod ("identity".AsSpan (), signature, out var fromByteSignature));
						Assert.AreEqual (identity.ID, fromBytes?.ID);
						Assert.AreEqual (identity.ID, fromByteSignature?.ID);
					}
				}
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		[DataRow (1, false)]
		[DataRow (1, true)]
		[DataRow (40, false)]
		public void RegisterNatives_SupplementaryUtf8 (int count, bool modified)
		{
			byte [] name = modified
				? [.. "native"u8, 0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80, 0]
				: [.. "native\U00010000"u8, 0];
			byte [] signature = modified
				? [.. "(Lnet/dot/jni/test/UnicodeNames$Value"u8, 0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80, .. ";)I"u8, 0]
				: [.. "(Lnet/dot/jni/test/UnicodeNames$Value\U00010000;)I"u8, 0];
			using (var type = new JniType (UnicodeTypeName)) {
				try {
					unsafe {
						// SAFETY: Both arrays remain pinned and terminated through registration; the callback has the matching JNI ABI.
						fixed (byte* namePtr = name)
						fixed (byte* signaturePtr = signature) {
							var methods = new JniNativeMethod [count];
							for (int i = 0; i < methods.Length; i++)
								methods [i] = new JniNativeMethod (namePtr, signaturePtr,
									(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>) &UnicodeNativeCallback);
							JniEnvironment.Types.RegisterNatives (type.PeerReference, methods);
						}
					}
					Assert.AreEqual (57, InvokeUnicodeNative (type));
				} finally {
					type.UnregisterNativeMethods ();
				}
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void RegisterNatives_MissingSupplementaryName_Throws ()
		{
			using (var type = new JniType (UnicodeTypeName)) {
				try {
					unsafe {
						// SAFETY: Literals remain pinned and terminated through registration; the callback has the matching JNI ABI.
						fixed (byte* name = "missing\U00010000"u8)
						fixed (byte* signature = "(Lnet/dot/jni/test/UnicodeNames$Value\U00010000;)I"u8) {
							var methods = new [] {
								new JniNativeMethod (name, signature,
									(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>) &UnicodeNativeCallback),
							};
							var error = Assert.ThrowsExactly<Java.Lang.NoSuchMethodError> (() => {
								unsafe {
									// SAFETY: Both pinned literals and the callback remain valid through this synchronous call.
									JniEnvironment.Types.RegisterNatives (type.PeerReference, methods);
								}
							});
							error.Dispose ();
						}
					}
				} finally {
					type.UnregisterNativeMethods ();
				}
			}
		}

		[UnmanagedCallersOnly]
		static int UnicodeNativeCallback (IntPtr env, IntPtr self, IntPtr value) => 57;

		internal static int InvokeUnicodeNative (JniType type)
		{
			var constructor = type.GetConstructor ("()V");
			// SAFETY: The constructor has no arguments and the class remains live.
			var instance = unsafe (type.NewObject (constructor, null));
			try {
				var method = type.GetInstanceMethod (UnicodeNativeName, UnicodeNativeSignature);
				unsafe {
					// SAFETY: One null object argument matches the registered descriptor.
					var args = stackalloc JniArgumentValue [1];
					args [0] = new JniArgumentValue (default (JniObjectReference));
					return JniEnvironment.InstanceMethods.CallIntMethod (instance, method, args);
				}
			} finally {
				JniObjectReference.Dispose (ref instance);
			}
		}

		[TestMethod]
		public void Sanity_Utf8 ()
		{
			// SAFETY: UTF-8 literals have NUL-terminated backing storage, and the type remains live.
			using (var Integer_class = unsafe (new JniType ("java/lang/Integer"u8))) {
				Assert.AreEqual ("java/lang/Integer", Integer_class.Name);

				// SAFETY: These terminated literals describe members of the live Integer type.
				var Integer_ctor     = unsafe (Integer_class.GetConstructor ("(I)V"u8));
				var Integer_intValue = unsafe (Integer_class.GetInstanceMethod ("intValue"u8, "()I"u8));
				JniObjectReference o;
				// SAFETY: The one-element stack buffer matches (I)V and is consumed synchronously.
				unsafe {
					var ctor_args = stackalloc JniArgumentValue [1];
					ctor_args [0] = new JniArgumentValue (42);
					o = Integer_class.NewObject (Integer_ctor, ctor_args);
				}
				try {
					// SAFETY: The live Integer peer's ()I method takes no arguments.
					int v = unsafe (JniEnvironment.InstanceMethods.CallIntMethod (o, Integer_intValue));
					Assert.AreEqual (42, v);
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}

		[TestMethod]
		public void FindClass_Utf8_ReturnsValidReference ()
		{
			// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
			var r = unsafe (JniEnvironment.Types.FindClass ("java/lang/Object"u8));
			try {
				Assert.IsTrue (r.IsValid);
			} finally {
				JniObjectReference.Dispose (ref r);
			}
		}

		[TestMethod]
		public void FindClass_Utf8_UsesSameFallbackAsStringOverload ()
		{
			var fromString = JniEnvironment.Types.FindClass ("java.lang.Object");
			// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
			var fromUtf8   = unsafe (JniEnvironment.Types.FindClass ("java.lang.Object"u8));
			try {
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString, fromUtf8));
			} finally {
				JniObjectReference.Dispose (ref fromString);
				JniObjectReference.Dispose (ref fromUtf8);
			}
		}

		[TestMethod]
		public void Ctor_Utf8_UsesSameFallbackAsStringOverload ()
		{
			using (var fromString = new JniType ("java.lang.Object"))
			// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
			using (var fromUtf8 = unsafe (new JniType ("java.lang.Object"u8))) {
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (fromString.PeerReference, fromUtf8.PeerReference));
				Assert.AreEqual ("java/lang/Object", fromUtf8.Name);
			}
		}

		[TestMethod]
		public void FindClass_Utf8_ThrowsOnNotFound ()
		{
			// SAFETY: The UTF-8 literal is terminated; only the class lookup is expected to fail.
			Assert.ThrowsExactly<Java.Lang.ClassNotFoundException> (() => unsafe (JniEnvironment.Types.FindClass ("does/not/Exist"u8)));
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void TryFindClass_Utf8_UsesSpanLength ()
		{
			// Neither slice is terminated at its length; only the span's bytes name the class.
			ReadOnlySpan<byte> longer = "java/lang/ObjectDoesNotExist"u8;
			ReadOnlySpan<byte> dottedLonger = "java.lang.ObjectDoesNotExist"u8;
			ReadOnlySpan<byte> prefix = "java/lang/Object"u8;
			ReadOnlySpan<byte> dottedPrefix = "java.lang.Object"u8;
			// SAFETY: Every slice comes from a live, NUL-terminated u8 literal.
			unsafe {
				AssertFindsObject (longer.Slice (0, 16));
				AssertFindsObject (dottedLonger.Slice (0, 16));
				Assert.IsFalse (JniEnvironment.Types.TryFindClass (prefix.Slice (0, 13), out var obj), "java/lang/Obj");
				Assert.IsFalse (obj.IsValid);
				Assert.IsFalse (JniEnvironment.Types.TryFindClass (dottedPrefix.Slice (0, 13), out obj), "java.lang.Obj");
				Assert.IsFalse (obj.IsValid);
				Exception? notFound = null;
				try {
					var found = JniEnvironment.Types.FindClass (prefix.Slice (0, 13));
					JniObjectReference.Dispose (ref found);
				} catch (Exception x) {
					notFound = x;
				}
				Assert.IsNotNull (notFound, "FindClass (java/lang/Obj) must not find java/lang/Object.");
			}
			Assert.IsFalse (JniEnvironment.Exceptions.ExceptionOccurred ().IsValid);

			static unsafe void AssertFindsObject (ReadOnlySpan<byte> classname)
			{
				var expected = JniEnvironment.Types.FindClass ("java/lang/Object");
				// SAFETY: classname is a slice of a live, NUL-terminated u8 literal.
				Assert.IsTrue (unsafe (JniEnvironment.Types.TryFindClass (classname, out var actual)));
				try {
					Assert.IsTrue (JniEnvironment.Types.IsSameObject (expected, actual));
				} finally {
					JniObjectReference.Dispose (ref expected);
					JniObjectReference.Dispose (ref actual);
				}
			}
		}

		[TestMethod]
		[TestCategory ("JniModifiedUtf8")]
		public void GetMembers_Utf8_UseSpanLength ()
		{
			using var Integer_class = new JniType ("java/lang/Integer");
			var r = Integer_class.PeerReference;
			// Every slice is unterminated at its length and must not include the bytes after it.
			ReadOnlySpan<byte> hashCode = "hashCodeX"u8;
			ReadOnlySpan<byte> intReturn = "()IX"u8;
			ReadOnlySpan<byte> valueOf = "valueOfX"u8;
			ReadOnlySpan<byte> valueOfSignature = "(I)Ljava/lang/Integer;X"u8;
			ReadOnlySpan<byte> value = "valueX"u8;
			ReadOnlySpan<byte> maxValue = "MAX_VALUEX"u8;
			ReadOnlySpan<byte> intType = "IX"u8;
			unsafe {
				// SAFETY: Every slice comes from a live, NUL-terminated u8 literal and r is a live class reference.
				Assert.IsTrue (JniEnvironment.InstanceMethods.GetMethodID (r, hashCode.Slice (0, 8), intReturn.Slice (0, 3)).IsValid);
				Assert.IsTrue (JniEnvironment.StaticMethods.GetStaticMethodID (r, valueOf.Slice (0, 7), valueOfSignature.Slice (0, valueOfSignature.Length - 1)).IsValid);
				Assert.IsTrue (JniEnvironment.InstanceFields.GetFieldID (r, value.Slice (0, 5), intType.Slice (0, 1)).IsValid);
				Assert.IsTrue (JniEnvironment.StaticFields.GetStaticFieldID (r, maxValue.Slice (0, 9), intType.Slice (0, 1)).IsValid);
				Assert.IsTrue (Integer_class.GetInstanceMethod (hashCode.Slice (0, 8), intReturn.Slice (0, 3)).IsValid);

				ReadOnlySpan<byte> hash = "hashCode"u8;
				Exception? notFound = null;
				try {
					_ = JniEnvironment.InstanceMethods.GetMethodID (r, hash.Slice (0, 4), "()I"u8);
				} catch (Exception e) {
					notFound = e;
				}
				Assert.IsNotNull (notFound, "GetMethodID (hash) must not find hashCode.");
			}
			Assert.IsFalse (JniEnvironment.Exceptions.ExceptionOccurred ().IsValid);
		}

		[TestMethod]
		public void TryFindClass_Utf8 ()
		{
			// SAFETY: Both UTF-8 literals have NUL-terminated backing storage.
			Assert.IsTrue (unsafe (JniEnvironment.Types.TryFindClass ("java/lang/Object"u8, out var found)));
			try {
				Assert.IsTrue (found.IsValid);
			} finally {
				JniObjectReference.Dispose (ref found);
			}

			Assert.IsFalse (unsafe (JniEnvironment.Types.TryFindClass ("does/not/Exist"u8, out var notFound)));
			Assert.IsFalse (notFound.IsValid);
		}

		[TestMethod]
		[TestCategory (JniReferenceLeakCategory)]
		public void TryFindClass_Utf8_DoesNotLeakGlobalRefs ()
		{
			AssertNoSustainedGlobalReferenceGrowth (() => {
				// SAFETY: The UTF-8 literal has NUL-terminated backing storage.
				Assert.IsFalse (unsafe (JniEnvironment.Types.TryFindClass ("does/not/Exist"u8, out var notFound)));
				Assert.IsFalse (notFound.IsValid);
			});
		}

		[TestMethod]
		[TestCategory (JniReferenceLeakCategory)]
		public void TryFindClass_String_DoesNotLeakGlobalRefs ()
		{
			AssertNoSustainedGlobalReferenceGrowth (() => {
				Assert.IsFalse (JniEnvironment.Types.TryFindClass ("does/not/Exist", out var notFound));
				Assert.IsFalse (notFound.IsValid);
			});
		}

		void AssertNoSustainedGlobalReferenceGrowth (Action action)
		{
			var context = TestContext ?? throw new InvalidOperationException ("The runner did not initialize TestContext.");
			JniReferenceLeakMeasurement.AssertNoSustainedGlobalReferenceGrowth (action, context.WriteLine);
		}

		[TestMethod]
		public void GetMethodID_Utf8_MatchesStringOverload ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var Object_class = unsafe (new JniType ("java/lang/Object"u8))) {
				var fromString = unsafe (JniEnvironment.InstanceMethods.GetMethodID (Object_class.PeerReference, "hashCode", "()I"));
				var fromUtf8   = unsafe (JniEnvironment.InstanceMethods.GetMethodID (Object_class.PeerReference, "hashCode"u8, "()I"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetStaticMethodID_Utf8_MatchesStringOverload ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var fromString = unsafe (JniEnvironment.StaticMethods.GetStaticMethodID (System_class.PeerReference, "currentTimeMillis", "()J"));
				var fromUtf8   = unsafe (JniEnvironment.StaticMethods.GetStaticMethodID (System_class.PeerReference, "currentTimeMillis"u8, "()J"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetFieldID_Utf8_MatchesStringOverload ()
		{
			// Integer.value is private and blocked by ART hidden API restrictions.
			// StreamTokenizer.ttype is a public instance field available on both JVM and Android.
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var StreamTokenizer_class = unsafe (new JniType ("java/io/StreamTokenizer"u8))) {
				var fromString = unsafe (JniEnvironment.InstanceFields.GetFieldID (StreamTokenizer_class.PeerReference, "ttype", "I"));
				var fromUtf8   = unsafe (JniEnvironment.InstanceFields.GetFieldID (StreamTokenizer_class.PeerReference, "ttype"u8, "I"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetStaticFieldID_Utf8_MatchesStringOverload ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout both lookups.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var fromString = unsafe (JniEnvironment.StaticFields.GetStaticFieldID (System_class.PeerReference, "in", "Ljava/io/InputStream;"));
				var fromUtf8   = unsafe (JniEnvironment.StaticFields.GetStaticFieldID (System_class.PeerReference, "in"u8, "Ljava/io/InputStream;"u8));
				Assert.AreEqual (fromString.ID, fromUtf8.ID);
			}
		}

		[TestMethod]
		public void GetStaticField_Utf8 ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live during field lookup.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var System_in = unsafe (System_class.GetStaticField ("in"u8, "Ljava/io/InputStream;"u8));
				Assert.IsNotNull (System_in);
				Assert.IsTrue (System_in.ID != IntPtr.Zero);
			}
		}

		[TestMethod]
		public void GetStaticMethod_Utf8_Invocation ()
		{
			// SAFETY: Literals are terminated, and the live type owns this no-argument static method.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				var currentTimeMillis = unsafe (System_class.GetStaticMethod ("currentTimeMillis"u8, "()J"u8));
				long time = unsafe (JniEnvironment.StaticMethods.CallStaticLongMethod (System_class.PeerReference, currentTimeMillis));
				Assert.IsTrue (time > 0);
			}
		}

		[TestMethod]
		public void GetCachedInstanceMethod_Utf8_ReturnsSameInstance ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout the cached lookups.
			using (var Object_class = unsafe (new JniType ("java/lang/Object"u8))) {
				JniMethodInfo? cached = null;
				var first  = unsafe (Object_class.GetCachedInstanceMethod (ref cached, "hashCode"u8, "()I"u8));
				var second = unsafe (Object_class.GetCachedInstanceMethod (ref cached, "hashCode"u8, "()I"u8));
				Assert.AreSame (first, second);
			}
		}

		[TestMethod]
		public void GetCachedStaticMethod_Utf8_ReturnsSameInstance ()
		{
			// SAFETY: Literals are terminated, and the owning type stays live throughout the cached lookups.
			using (var System_class = unsafe (new JniType ("java/lang/System"u8))) {
				JniMethodInfo? cached = null;
				var first  = unsafe (System_class.GetCachedStaticMethod (ref cached, "currentTimeMillis"u8, "()J"u8));
				var second = unsafe (System_class.GetCachedStaticMethod (ref cached, "currentTimeMillis"u8, "()J"u8));
				Assert.AreSame (first, second);
			}
		}

		[TestMethod]
		public void Dispose_Exceptions_Utf8 ()
		{
			// SAFETY: All UTF-8 inputs below are terminated literals; disposed-type checks precede JNI dispatch.
			var t = unsafe (new JniType ("java/lang/Object"u8));
			t.Dispose ();

			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetConstructor ("()V"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetInstanceField ("value"u8, "I"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetInstanceMethod ("hashCode"u8, "()I"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetStaticField ("in"u8, "Ljava/io/InputStream;"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetStaticMethod ("currentTimeMillis"u8, "()J"u8)));

			JniFieldInfo?  jif = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedInstanceField (ref jif, "value"u8, "I"u8)));
			JniMethodInfo? jim = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedConstructor (ref jim, "()V"u8)));
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedInstanceMethod (ref jim, "hashCode"u8, "()I"u8)));
			JniFieldInfo?  jsf = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedStaticField (ref jsf, "in"u8, "Ljava/io/InputStream;"u8)));
			JniMethodInfo? jsm = null;
			Assert.ThrowsExactly<ObjectDisposedException> (() => unsafe (t.GetCachedStaticMethod (ref jsm, "currentTimeMillis"u8, "()J"u8)));
		}

		[TestMethod]
		public void InvalidSignature_Utf8_ThrowsJniException ()
		{
			// SAFETY: Literals are terminated and the live type owns the lookup; the absent method must fail.
			using (var Object_class = unsafe (new JniType ("java/lang/Object"u8))) {
				Assert.ThrowsExactly<Java.Lang.NoSuchMethodError> (() => unsafe (Object_class.GetInstanceMethod ("bogus"u8, "(Z)V"u8)));
			}
		}
	}
}
